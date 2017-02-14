using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Serialization;
using System.Xml;
using System.Text;

using Alphaleonis.Win32.Filesystem;


namespace Synapse.Handlers.Legacy.SQLCommand
{
	[Serializable, XmlRoot( "SQLCommand" )]
	public class WorkflowParameters
	{
        #region Public Global Workflow Parameters

        private List<ParameterType> _parameters = new List<ParameterType>();

        [XmlElement]
        public OracleType Oracle;
        [XmlElement]
        public SQLServerType SQLServer;
        [XmlElement]
        public String RunAsUser;
        [XmlElement]
        public String RunAsPassword;
        [XmlElement]
        public String Query;
        [XmlElement]
        public String StoredProcedure;
        [XmlElement]
        public OutputFileType OutputFile;

        [XmlArrayItem(ElementName = "Parameter")]
        public List<ParameterType> Parameters
        {
            get { return _parameters; }
            set { _parameters = value; }
        }
        
        #endregion
        #region Validation Flags

        [XmlIgnore]
        public bool IsValidDatabaseType { get; protected set; }
        [XmlIgnore]
        public bool IsValid { get; protected set; }
        
        #endregion
        #region Public Workflow Parameter Methods

		public virtual void PrepareAndValidate() 
        {
            IsValidDatabaseType = true;
            IsValid = true;

            if (!((Oracle != null) ^ (SQLServer != null)))
                IsValidDatabaseType = false;

            IsValid = IsValidDatabaseType;
        }

		public virtual void Serialize(string filePath)
		{
			Utils.Serialize<WorkflowParameters>( this, true, filePath );
		}

        public virtual String Serialize(bool indented = true)
        {
            return Utils.Serialize<WorkflowParameters>(this, indented);
        }

        public static WorkflowParameters Deserialize(XmlElement el)
        {
            XmlSerializer s = new XmlSerializer(typeof(WorkflowParameters));
            return (WorkflowParameters)s.Deserialize(new System.IO.StringReader(el.OuterXml));
        }
        
        public static WorkflowParameters Deserialize(string filePath)
		{
			return Utils.DeserializeFile<WorkflowParameters>( filePath );
		}

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine(">> SQLCommand");
            sb.AppendLine("   >> Query         : " + this.Query);
            sb.AppendLine("   >> StoredProc    : " + this.StoredProcedure);
            sb.AppendLine("   >> RunAsUser     : " + this.RunAsUser);
            sb.AppendLine("   >> RunAsPassword : " + this.RunAsPassword);
            if (this.Oracle != null)
                sb.Append(this.Oracle.ToString());
            if (this.SQLServer != null)
                sb.Append(this.SQLServer.ToString());
            if (this.OutputFile != null)
                sb.Append(this.OutputFile.ToString());
            if (this.Parameters != null)
            {
                sb.AppendLine("   >> Parameters");
                foreach (ParameterType parameter in Parameters)
                    sb.Append(parameter.ToString());
            }

            return sb.ToString();
        }

        #endregion
    }

    public class OracleType
    {
        [XmlElement]
        public string User;
        [XmlElement]
        public string Password;
        [XmlElement]
        public string DataSource;

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("   >> Oracle");
            sb.AppendLine("      >> User       : " + this.User);
            sb.AppendLine("      >> Password   : " + this.Password);
            sb.AppendLine("      >> DataSource : " + this.DataSource);

            return sb.ToString();
        }
    }

    public class SQLServerType
    {
        [XmlElement]
        public string User;
        [XmlElement]
        public string Password;
        [XmlElement]
        public string DataSource;
        [XmlElement("IntegratedSecurity")]
        public String IntegratedSecurityStr
        {
            get { return IntegratedSecurity.ToString(); }
            set { try { IntegratedSecurity = Boolean.Parse(value); } catch (Exception) { } }
        }
        [XmlElement("TrustedConnection")]
        public String TrustedConnectionStr
        {
            get { return TrustedConnection.ToString(); }
            set { try { TrustedConnection = Boolean.Parse(value); } catch (Exception) { } }
        }

        [XmlElement]
        public string Database;
        [XmlElement]
        public int ConnectionTimeout;

        [XmlIgnore]
        public bool IntegratedSecurity = false;
        [XmlIgnore]
        public bool TrustedConnection = false;


        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("   >> SQLServer");
            sb.AppendLine("      >> User       : " + this.User);
            sb.AppendLine("      >> Password   : " + this.Password);
            sb.AppendLine("      >> DataSource : " + this.DataSource);
            sb.AppendLine("      >> IntSecurty : " + this.IntegratedSecurity);
            sb.AppendLine("      >> TrustdConn : " + this.TrustedConnection);
            sb.AppendLine("      >> Database   : " + this.Database);
            sb.AppendLine("      >> ConTimeout : " + this.ConnectionTimeout);

            return sb.ToString();
        }

    }

    public class OutputFileType
    {
        [XmlText]
        public string Value;
        [XmlAttribute]
        public string Delimeter = ",";
        [XmlAttribute("ShowResults")]
        public string ShowResultsStr
        {
            get { return ShowResults.ToString(); }
            set { try { ShowResults = Boolean.Parse(value); } catch (Exception) { } }
        }

        [XmlAttribute("ShowColumnNames")]
        public string ShowColumnNamesStr
        {
            get { return ShowColumnNames.ToString(); }
            set { try { ShowColumnNames = Boolean.Parse(value); } catch (Exception) { } }
        }

        [XmlAttribute("Append")]
        public string AppendStr
        {
            get { return Append.ToString(); }
            set { try { Append = Boolean.Parse(value); } catch (Exception) { } }
        }

        [XmlAttribute("MergeResults")]
        public string MergeResultsStr
        {
            get { return MergeResults.ToString(); }
            set { try { MergeResults = Boolean.Parse(value); } catch (Exception) { } }
        }

        [XmlIgnore]
        public bool ShowResults = false;
        [XmlIgnore]
        public bool ShowColumnNames = true;
        [XmlIgnore]
        public bool Append = false;
        [XmlIgnore]
        public bool MergeResults = false;

        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("   >> OutputFile    : " + this.Value);
            sb.AppendLine("      >> Delimeter  : " + this.Delimeter);
            sb.AppendLine("      >> ShowReslts : " + this.ShowResults);
            sb.AppendLine("      >> ShowColNms : " + this.ShowColumnNames);
            sb.AppendLine("      >> Append     : " + this.Append);
            sb.AppendLine("      >> MergeRslts : " + this.MergeResults);

            return sb.ToString();
        }
    }

    public class ParameterType
    {
        [XmlAttribute]
        public int SortIndex;
        [XmlElement]
        public System.Data.ParameterDirection Direction = System.Data.ParameterDirection.Input;
        [XmlElement]
        public SqlParamterTypes Type;
        [XmlElement]
        public int Size;
        [XmlElement]
        public string Name;
        [XmlElement]
        public string Value;

        [XmlAttribute]
        public string OutputFile;
        [XmlAttribute]
        public string Delimeter;
        [XmlAttribute("ShowResults")]
        public string ShowResultsStr
        {
            get { return _showResultsStr; }
            set { try { _showResultsStr = value; ShowResults = Boolean.Parse(value); } catch (Exception) { } }
        }

        [XmlAttribute("ShowColumnNames")]
        public string ShowColumnNamesStr
        {
            get { return _showColumnNamesStr; }
            set { try { _showColumnNamesStr = value; ShowColumnNames = Boolean.Parse(value); } catch (Exception) { } }
        }

        [XmlAttribute("Append")]
        public string AppendStr
        {
            get { return _appendstr; }
            set { try { _appendstr = value; Append = Boolean.Parse(value); } catch (Exception) { } }
        }

        [XmlAttribute("MergeResults")]
        public string MergeResultsStr
        {
            get { return _mergeResultsStr; }
            set { try { _mergeResultsStr = value; MergeResults = Boolean.Parse(value); } catch (Exception) { } }
        }

        [XmlIgnore]
        public bool ShowResults = false;
        [XmlIgnore]
        public bool ShowColumnNames = true;
        [XmlIgnore]
        public bool Append = false;
        [XmlIgnore]
        public bool MergeResults = false;
        [XmlIgnore]
        public string _showResultsStr;
        [XmlIgnore]
        public string _showColumnNamesStr;
        [XmlIgnore]
        public string _appendstr;
        [XmlIgnore]
        public string _mergeResultsStr;



        public override String ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("      >> Parameter");
            sb.AppendLine("         >> Index      : " + this.SortIndex);
            sb.AppendLine("         >> Name       : " + this.Name);
            sb.AppendLine("         >> Value      : " + this.Value);
            sb.AppendLine("         >> Type       : " + this.Type);
            sb.AppendLine("         >> Size       : " + this.Size);
            sb.AppendLine("         >> Direction  : " + this.Direction);
            sb.AppendLine("         >> Outfile    : " + this.OutputFile);
            sb.AppendLine("         >> Delimeter  : " + this.Delimeter);
            sb.AppendLine("         >> ShwReslts  : " + this.ShowResultsStr);
            sb.AppendLine("         >> ShwColNams : " + this.ShowColumnNamesStr);
            sb.AppendLine("         >> Append     : " + this.AppendStr);
            sb.AppendLine("         >> MergeRslts : " + this.MergeResults);

            return sb.ToString();
        }

    }


}