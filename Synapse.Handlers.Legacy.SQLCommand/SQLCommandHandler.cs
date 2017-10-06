using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Xml;
using System.Xml.Serialization;
using System.IO;

using Synapse.Handlers.Legacy.SQLCommand;

using Synapse.Core;

public class SQLCommandHandler : HandlerRuntimeBase
{
    int seqNo = 0;
    override public ExecuteResult Execute(HandlerStartInfo startInfo)
    {
        XmlSerializer ser = new XmlSerializer(typeof(WorkflowParameters));
        WorkflowParameters wfp = new WorkflowParameters();
        TextReader reader = new StringReader(startInfo.Parameters);
        wfp = (WorkflowParameters)ser.Deserialize(reader);

        Workflow wf = null;
        if (wfp.Oracle != null)
            wf = new OracleWorkflow(wfp);
        else if (wfp.SQLServer != null)
            wf = new SQLServerWorkflow(wfp);
        else
            wf = new Workflow(wfp);

        wf.OnLogMessage = this.OnLogMessage;
        wf.OnProgress = this.OnProgress;

        seqNo = 0;
        OnProgress("Execute", "Starting", StatusType.Running, startInfo.InstanceId, seqNo++);
        wf.ExecuteAction(startInfo);

        return new ExecuteResult() { Status = StatusType.Complete };
    }

    public override object GetConfigInstance()
    {
        return null;
    }

    public override object GetParametersInstance()
    {
        WorkflowParameters wfp = new WorkflowParameters();

        wfp.Oracle = new OracleType();
        wfp.Oracle.User = "scott";
        wfp.Oracle.Password = "tiger";
        wfp.Oracle.DataSource = @"(DESCRIPTION = (ADDRESS = (PROTOCOL = TCP)(HOST = localhost)(PORT = 1521))(CONNECT_DATA =(SERVER = DEDICATED)(SERVICE_NAME = XE)))";

        wfp.SQLServer = new SQLServerType();
        wfp.SQLServer.ConnectionTimeout = 30;
        wfp.SQLServer.Database = "SANDBOX";
        wfp.SQLServer.DataSource = @"Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;";
        wfp.SQLServer.IntegratedSecurity = true;
        wfp.SQLServer.Password = "MyPassword";
        wfp.SQLServer.TrustedConnection = true;
        wfp.SQLServer.User = "MyUserName";

        wfp.Query = "SELECT * from USERS where NAME = @userName";
        wfp.StoredProcedure = "dbo.uspGetUsers";

        wfp.Parameters = new List<ParameterType>();
        ParameterType param = new ParameterType();
        param.Name = "userName";
        param.Value = "Bill Gates";
        param.Direction = System.Data.ParameterDirection.Input;
        param.Type = SqlParamterTypes.String;
        wfp.Parameters.Add( param );

        String xml = wfp.Serialize( false );
        xml = xml.Substring( xml.IndexOf( "<" ) );
        return xml;

    }
}
