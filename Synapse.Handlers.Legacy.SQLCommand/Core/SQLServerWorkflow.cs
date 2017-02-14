using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data.Common;

using config = Synapse.Handlers.Legacy.SQLCommand.Properties.Settings;

namespace Synapse.Handlers.Legacy.SQLCommand
{
    public class SQLServerWorkflow : Workflow
    {
        public SQLServerWorkflow(WorkflowParameters wfp) : base(wfp)
		{
		}

        public override void RunMainWorkflow()
        {
            OnStepProgress("RunMainWorkflow", "Starting Main SQLServer Workflow");

            String commandText = "";
            bool isStoredProc = false;
            if (!String.IsNullOrWhiteSpace(_wfp.Query))
                commandText = _wfp.Query;
            else if (!String.IsNullOrWhiteSpace(_wfp.StoredProcedure))
            {
                commandText = _wfp.StoredProcedure;
                isStoredProc = true;
            }

            SqlConnection con = BuildConnection();
            DbDataReader reader = ExecuteCommand(con, commandText, isStoredProc);
            ParseResults(reader);

            con.Close();
            con.Dispose();
        }

        private SqlConnection BuildConnection()
        {
            SqlConnection con = new SqlConnection();

            StringBuilder sb = new StringBuilder();
            if (!String.IsNullOrWhiteSpace(_wfp.SQLServer.User))
                sb.Append(@"user id=" + _wfp.SQLServer.User + ";");
            if (!String.IsNullOrWhiteSpace(_wfp.SQLServer.Password))
                sb.Append(@"password=" + DecryptPassword(_wfp.SQLServer.Password) + ";");
            if (!String.IsNullOrWhiteSpace(_wfp.SQLServer.DataSource))
                sb.Append(@"data source=" + _wfp.SQLServer.DataSource + ";");
            if (_wfp.SQLServer.IntegratedSecurity)
                sb.Append(@"Integrated Security=SSPI;");
            if (_wfp.SQLServer.TrustedConnection)
                sb.Append(@"Trusted_Connection=yes;");
            if (!String.IsNullOrWhiteSpace(_wfp.SQLServer.Database))
                sb.Append(@"database=" + _wfp.SQLServer.Database + ";");
            if (_wfp.SQLServer.ConnectionTimeout > 0)
                sb.Append(@"connection timeout=" + _wfp.SQLServer.ConnectionTimeout + ";");

            con.ConnectionString = sb.ToString();

            return con;
        }

        public override DbParameter AddParameter(DbCommand cmd, String name, String value, SqlParamterTypes type, int size, System.Data.ParameterDirection direction)
        {
            SqlParameter param = new SqlParameter();
            SqlCommand command = (SqlCommand)cmd;
            param.ParameterName = name;
            param.Value = value;
            param.Direction = direction;
            param.Size = size;

            param.DbType = (System.Data.DbType)Enum.Parse(typeof(System.Data.DbType), type.ToString());

            command.Parameters.Add(param);
            return param;
        }

        public override DbCommand BuildCommand(DbConnection con, String commandText)
        {
            SqlCommand command = new SqlCommand();
            command.Connection = (SqlConnection)con;
            command.CommandText = commandText;
            return command;
        }

        public override void ParseParameter(DbParameter parameter)
        {
            SqlParameter param = (SqlParameter)parameter;
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
                WriteParameter(parameter.ParameterName, parameter.Value, fileName, showColumnNames, appendToFile);
            }
        }
    }
}
