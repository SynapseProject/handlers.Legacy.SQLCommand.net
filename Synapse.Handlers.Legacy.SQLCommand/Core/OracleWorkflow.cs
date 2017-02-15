using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography.Utility;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data.Common;


using config = Synapse.Handlers.Legacy.SQLCommand.Properties.Settings;


namespace Synapse.Handlers.Legacy.SQLCommand
{
    public class OracleWorkflow : Workflow
    {
        public OracleWorkflow(WorkflowParameters wfp) : base(wfp)
		{
		}

        public override void RunMainWorkflow(bool isDryRun)
        {
            String commandText = "";
            bool isStoredProc = false;
            OnStepProgress("RunMainWorkflow", "Starting Main Oracle Workflow");
            if (!String.IsNullOrWhiteSpace(_wfp.Query))
                commandText = _wfp.Query;
            else if (!String.IsNullOrWhiteSpace(_wfp.StoredProcedure))
            {
                commandText = _wfp.StoredProcedure;
                isStoredProc = true;
            }

            OracleConnection con = BuildConnection();
            DbDataReader reader = ExecuteCommand(con, commandText, isStoredProc, isDryRun);
            ParseResults(reader);

            con.Close();
            con.Dispose();
        }

        private OracleConnection BuildConnection()
        {
            OracleConnection con = new OracleConnection();

            StringBuilder sb = new StringBuilder();
            if (!String.IsNullOrWhiteSpace(_wfp.Oracle.User))
                sb.Append(@"user id=" + _wfp.Oracle.User + ";");
            if (!String.IsNullOrWhiteSpace(_wfp.Oracle.Password))
                sb.Append(@"password=" + DecryptPassword(_wfp.Oracle.Password) + ";");
            if (!String.IsNullOrWhiteSpace(_wfp.Oracle.DataSource))
                sb.Append(@"data source=" + _wfp.Oracle.DataSource + ";");

            con.ConnectionString = sb.ToString();

            return con;
        }

        public override DbParameter AddParameter(DbCommand cmd, String name, String value, SqlParamterTypes type, int size, System.Data.ParameterDirection direction)
        {
            OracleParameter param = new OracleParameter();
            OracleCommand command = (OracleCommand)cmd;
            param.ParameterName = name;
            param.Direction = direction;
            param.Value = value;
            param.Size = size;

            int enumValue = (int)type;
            if (enumValue >= 100)
                param.OracleDbType = (OracleDbType)Enum.Parse(typeof(OracleDbType), type.ToString());
            else 
                param.DbType = (System.Data.DbType)Enum.Parse(typeof(System.Data.DbType), type.ToString());
            
            // For Oracle Functions, ReturnValue Must Be First Parameter
            if (param.Direction == System.Data.ParameterDirection.ReturnValue)
                command.Parameters.Insert(0, param);
            else
                command.Parameters.Add(param);
            return param;
        }

        public override DbCommand BuildCommand(DbConnection con, String commandText)
        {
            OracleCommand command = new OracleCommand();
            command.Connection = (OracleConnection)con;
            command.CommandText = commandText;
            return command;
        }

        public override void ParseParameter(DbParameter parameter)
        {
            OracleParameter param = (OracleParameter)parameter;
            ParameterType wfpParam = GetParameterType(parameter.ParameterName);

            String fileName = null;
            String delimeter = ",";
            bool showResults = true;
            bool showColumnNames = true;
            bool appendToFile = false;
            bool mergeResults = false;

            if (_wfp.OutputFile != null)
            {
                fileName = _wfp.OutputFile.Value;
                delimeter = _wfp.OutputFile.Delimeter;
                showResults = _wfp.OutputFile.ShowResults;
                showColumnNames = _wfp.OutputFile.ShowColumnNames;
                appendToFile = _wfp.OutputFile.Append;
                mergeResults = _wfp.OutputFile.MergeResults;
            }

            if (wfpParam != null)
            {
                if (!String.IsNullOrWhiteSpace(wfpParam.OutputFile))
                    fileName = wfpParam.OutputFile;
                else if (wfpParam.Direction != System.Data.ParameterDirection.ReturnValue)
                    fileName = "";

                if (!String.IsNullOrWhiteSpace(wfpParam.Delimeter))
                    delimeter = wfpParam.Delimeter;
                if (!String.IsNullOrWhiteSpace(wfpParam.ShowResultsStr))
                    showResults = wfpParam.ShowResults;
                if (!String.IsNullOrWhiteSpace(wfpParam.ShowColumnNamesStr))
                    showColumnNames = wfpParam.ShowColumnNames;
                if (!String.IsNullOrWhiteSpace(wfpParam.AppendStr))
                    appendToFile = wfpParam.Append;
                if (!String.IsNullOrWhiteSpace(wfpParam.MergeResultsStr))
                    mergeResults = wfpParam.MergeResults;
            }
            
            if (parameter.Direction != System.Data.ParameterDirection.Input)
            {
                OnStepProgress("Results", param.Direction + " Parameter - [" + param.ParameterName + "] = [" + param.Value + "]");

                if (param.OracleDbType == OracleDbType.RefCursor)
                {
                    OracleDataReader reader = ((OracleRefCursor)param.Value).GetDataReader();
                    ParseResults(reader, fileName, delimeter, showColumnNames, showResults, appendToFile, mergeResults);
                }
                else
                {
                    WriteParameter(parameter.ParameterName, parameter.Value, fileName, showColumnNames, appendToFile);
                }
            }
        }



    }
}
