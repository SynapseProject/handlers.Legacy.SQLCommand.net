using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Xml;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Common;
using System.IO;
using System.Text.RegularExpressions;
using System.Security.Cryptography.Utility;

using Alphaleonis.Win32.Filesystem;
using Synapse.Core;


using config = Synapse.Handlers.Legacy.SQLCommand.Properties.Settings;

namespace Synapse.Handlers.Legacy.SQLCommand
{
	public class Workflow
	{
		protected WorkflowParameters _wfp = null;
        public Action<string, string, LogLevel, Exception> OnLogMessage;
        public Func<string, string, StatusType, long, int, bool, Exception, bool> OnProgress;

        public Workflow(WorkflowParameters wfp)
		{
			_wfp = wfp;
		}

		public WorkflowParameters Parameters { get { return _wfp; } set { _wfp = value as WorkflowParameters; } }

		public void ExecuteAction()
		{
			string context = "ExecuteAction";

			string msg = Utils.GetHeaderMessage( string.Format( "Entering Main Workflow.") );
			if( OnStepStarting( context, msg ) )
			{
				return;
			}

            OnStepProgress(context, _wfp.Serialize(false));
            Stopwatch clock = new Stopwatch();
            clock.Start();

            Exception ex = null;
            try
            {
                bool isValid = ValidateParameters();

                if (isValid)
                {
                    RunMainWorkflow();
                }
                else
                {
                    OnStepProgress(context, "Package Validation Failed");
                }
            }
            catch (Exception exception)
            {
                ex = exception;
                OnStepProgress("ERROR", exception.Message);
            }

            bool ok = ex == null;
            msg = Utils.GetHeaderMessage(string.Format("End Main Workflow: {0}, Total Execution Time: {1}",
                ok ? "Complete." : "One or more steps failed.", clock.ElapsedSeconds()));
            OnProgress(context, msg, ok ? StatusType.Complete : StatusType.Failed, 0, int.MaxValue, false, ex);

        }

        public virtual void RunMainWorkflow()
        {
            OnStepProgress("RunMainWorkflow", @"Unable to determine database type.  Please specify Oracle or SQLServer.");
        }

        bool ValidateParameters()
        {
            string context = "Validate";
            const int padding = 50;

            OnStepProgress(context, Utils.GetHeaderMessage("Begin [PrepareAndValidate]"));

            _wfp.PrepareAndValidate();

            OnStepProgress(context, Utils.GetMessagePadRight("WorkflowParameters.IsValidDatabaseType", _wfp.IsValidDatabaseType, padding));
            OnStepProgress(context, Utils.GetMessagePadRight("WorkflowParameters.IsValid", _wfp.IsValid, padding));
            OnStepProgress(context, Utils.GetHeaderMessage("End [PrepareAndValidate]"));

            return _wfp.IsValid;
        }

        protected DbDataReader ExecuteCommand(DbConnection con, String cmdText, bool isStoredProc=false)
        {
            bool useImpersonation = false;
            Impersonator user = null;

            if (!String.IsNullOrWhiteSpace(_wfp.RunAsUser))
            {
                String password = DecryptPassword(_wfp.RunAsPassword);

                user = new Impersonator(config.Default.DefaultRunAsDomain, _wfp.RunAsUser, password);
                user.StartImpersonation();
                useImpersonation = true;
            }

            DbCommand command = BuildCommand(con, cmdText);

            String connString = con.ConnectionString;
            connString = Regex.Replace(connString, @";password=.*?;", @";password=********;");
            OnStepProgress("ExecuteQuery", "Connection String - " + connString);

            if (isStoredProc)
            {
                command.CommandType = System.Data.CommandType.StoredProcedure;
                OnStepProgress("ExecuteQuery", "Stored Procedure - " + command.CommandText);
            }
            else
                OnStepProgress("ExecuteQuery", "Query - " + command.CommandText);
                        
            if (_wfp.Parameters != null)
            {
                bool needsSorting = false;
                foreach (ParameterType parameter in _wfp.Parameters)
                {
                    if (parameter.SortIndex != 0)
                    {
                        needsSorting = true;
                        break;
                    }

                }

                if (needsSorting)
                {
                    _wfp.Parameters.Sort(delegate(ParameterType x, ParameterType y)
                    {
                        return x.SortIndex.CompareTo(y.SortIndex);
                    });
                }

                foreach (ParameterType parameter in _wfp.Parameters)
                {
                    AddParameter(command, parameter.Name, parameter.Value, parameter.Type, parameter.Size, parameter.Direction);
                    OnStepProgress("ExeucteQuery", parameter.Direction + " Paramter - [" + parameter.Name + "] = [" + parameter.Value + "]");
                }
            }

            DbDataReader reader = null;
            try
            {
                con.Open();
                if (isStoredProc && (this.GetType() == typeof(OracleWorkflow)))
                {
                    command.ExecuteNonQuery();
                }
                else
                {
                    reader = command.ExecuteReader();
                }

                // Log Any Output Parameters From Call
                foreach (DbParameter parameter in command.Parameters)
                {
                    ParseParameter(parameter);
                }
            }
            catch (Exception e)
            {
                OnStepProgress("ExecuteQuery", "ERROR : " + e.Message);
                throw e;
            }

            if (useImpersonation)
                user.StopImpersonation();

            return reader;
        }

        public virtual DbParameter AddParameter(DbCommand cmd, String name, String value, SqlParamterTypes type, int size, System.Data.ParameterDirection direction)
        {
            OnStepProgress("BuildParameter", @"Unknown database type.  Can not create parameter.");
            return null;
        }

        public virtual DbCommand BuildCommand(DbConnection con, String commandText)
        {
            OnStepProgress("BuildCommand", @"Unknown database type.  Can not create command.");
            throw new Exception("Unknown Connection Type [" + con.GetType() + "]");
        }

        public virtual void ParseParameter(DbParameter parameter)
        {
            if (parameter.Direction != System.Data.ParameterDirection.Input)
                OnStepProgress("Results", parameter.Direction + " Parameter - [" + parameter.ParameterName + "] = [" + parameter.Value + "]");
        }

        protected void ParseResults(DbDataReader reader)
        {
            String delimeter = ",";
            bool showColumnNames = true;
            bool showResults = true;
            bool appendToFile = false;
            bool mergeResults = false;
            String fileName = null;

            if (reader == null)
                return;

            if (_wfp.OutputFile != null)
            {
                delimeter = _wfp.OutputFile.Delimeter;
                showColumnNames = _wfp.OutputFile.ShowColumnNames;
                showResults = _wfp.OutputFile.ShowResults;
                appendToFile = _wfp.OutputFile.Append;
                fileName = _wfp.OutputFile.Value;
                mergeResults = _wfp.OutputFile.MergeResults;
            }

            ParseResults(reader, fileName, delimeter, showColumnNames, showResults, appendToFile, mergeResults);

        }

        protected void ParseResults(DbDataReader reader, String fileName, String delimeter, bool showColumnNames, bool showResults, bool appendToFile, bool mergeResults)
        {
            StreamWriter writer = null;
            if (reader == null)
                return;

            if (reader.HasRows)
            {
                int totalSets = 0;
                do
                {
                    if (!String.IsNullOrWhiteSpace(fileName))
                    {
                        String setFileName = fileName;
                        bool doAppend = appendToFile;
                        if (totalSets > 0 && !mergeResults)
                        {
                            String filePath = System.IO.Path.GetDirectoryName(fileName);
                            String fileRoot = System.IO.Path.GetFileNameWithoutExtension(fileName);
                            String fileExt = System.IO.Path.GetExtension(fileName);
                            setFileName = String.Format(@"{0}\{1}_{2:D3}{3}", filePath, fileRoot, (totalSets+1), fileExt);
                        }

                        // Merge Results Means Append To File
                        if (totalSets > 0 && mergeResults)
                            doAppend = true;

                        writer = new StreamWriter(setFileName, doAppend);
                        OnStepProgress("Results", "OutputFile - [" + setFileName + "]");
                    }

                    StringBuilder sb = new StringBuilder();
                    if (showColumnNames)
                    {
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            sb.Append(reader.GetName(i));
                            if (i != (reader.FieldCount - 1))
                                sb.Append(delimeter);
                        }
                        WriteRow(sb.ToString(), writer, showResults);
                    }


                    int totalRows = 0;
                    while (reader.Read())
                    {
                        sb.Clear();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            Type type = reader[i].GetType();
                            String field = FormatData(reader.GetFieldType(i), reader.GetValue(i));
                            sb.Append(field);
                            if (i != (reader.FieldCount - 1))
                                sb.Append(delimeter);
                        }

                        totalRows++;
                        WriteRow(sb.ToString(), writer, showResults);
                    }

                    OnStepProgress("Results", "Total Records : " + totalRows);

                    if (writer != null)
                    {
                        writer.Close();
                    }

                    totalSets++;

                } while (reader.NextResult());

            }

        }

        protected void WriteParameter(String name, Object value, String fileName, bool showColumnNames, bool appendToFile)
        {
            StreamWriter writer = null;

            if (!String.IsNullOrWhiteSpace(fileName))
            {
                writer = new StreamWriter(fileName, appendToFile);
                OnStepProgress("Results", "OutputFile - [" + fileName + "]");

                if (showColumnNames)
                    writer.WriteLine(name);
                if (value == null)
                    writer.WriteLine("");
                else
                    writer.WriteLine(value.ToString());

                writer.Close();
                writer.Dispose();
            }
        }

        private String FormatData(Type type, Object field)
        {
            String data = field.ToString();

            if (type == typeof(String))
                data = @"""" + field.ToString() + @"""";

            return data;
        }

        private void WriteRow(String line, StreamWriter file, bool showResults=true)
        {
            if (file == null)
            {
                OnStepProgress("Results", line);
            }
            else
            {
                file.WriteLine(line);
                if (showResults)
                    OnStepProgress("Results", line);
            }
        }

        protected string DecryptPassword(string password)
        {
            Cipher cipher = new Cipher(config.Default.PassPhrase, config.Default.SaltValue, config.Default.InitVector);
            String pwd = cipher.Decrypt(password);
            if (pwd.StartsWith("UNABLE TO DECRYPT"))
                return password;
            else
                return pwd;
        }

        protected ParameterType GetParameterType(String name)
        {
            ParameterType retParam = null;

            if (_wfp.Parameters != null)
                foreach (ParameterType param in _wfp.Parameters)
                    if (param.Name.Equals(name))
                    {
                        retParam = param;
                        break;
                    }

            return retParam;
        }



        #region NotifyProgress Events
		int _cheapSequence = 0;

		void p_StepProgress(object sender, AdapterProgressEventArgs e)
		{
            OnProgress(e.Context, e.Message, StatusType.Running, 0, _cheapSequence++, false, e.Exception);
		}

		/// <summary>
		/// Notify of step beginning. If return value is True, then cancel operation.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name.</param>
		/// <param name="message">Descriptive message.</param>
		/// <returns>AdapterProgressCancelEventArgs.Cancel value.</returns>
		bool OnStepStarting(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, 0, _cheapSequence++, false, null);
			return false;
		}

		/// <summary>
		/// Notify of step progress.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name.</param>
		/// <param name="message">Descriptive message.</param>
		protected void OnStepProgress(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, 0, _cheapSequence++, false, null);
		}

		/// <summary>
		/// Notify of step completion.
		/// Defaults: PackageStatus.Running, Id = _cheapSequence++, Severity = 0, Exception = null.
		/// </summary>
		/// <param name="context">The method name or workflow activty.</param>
		/// <param name="message">Descriptive message.</param>
		protected void OnStepFinished(string context, string message)
		{
            OnProgress(context, message, StatusType.Running, 0, _cheapSequence++, false, null);
		}
		#endregion

	}

}