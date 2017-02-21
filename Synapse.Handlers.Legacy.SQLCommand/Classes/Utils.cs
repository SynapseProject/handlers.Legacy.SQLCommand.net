using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using System.Collections;

using System.Security.Cryptography.Utility;

using fs = Alphaleonis.Win32.Filesystem;

using config = Synapse.Handlers.Legacy.SQLCommand.Properties.Settings;

namespace Synapse.Handlers.Legacy.SQLCommand
{
	public static class Utils
	{
        public static string escQuote = "'";

		/// <summary>
		/// Checks for the existence of the file and returns the full path to file.
		/// </summary>
		/// <param name="path">Path to file to validate.</param>
		/// <param name="exists">Returns reslt from File.Exists( path )</param>
		/// <returns>Returns  if it exists.</returns>
		public static string FormatPath(string path, out bool exists)
		{
			exists = fs.File.Exists( path );
			if( exists )
			{
				//path = new FileInfo( path ).FullName;
				path = fs.Path.GetFullPath( path );
			}

			return path;
		}

		const string _lines = "--------------------------";
		public static double ElapsedSeconds(this Stopwatch stopwatch)
		{
			return TimeSpan.FromMilliseconds( stopwatch.ElapsedMilliseconds ).TotalSeconds;
		}

		public static string GetMessagePadLeft(string header, object message, int width)
		{
			return string.Format( "{0}: {1}", header.PadLeft( width, '.' ), message );
		}

		public static string GetMessagePadRight(string header, object message, int width)
		{
			return string.Format( "{0}: {1}", header.PadRight( width, '.' ), message );
		}

		public static string GetHeaderMessage(string header)
		{
			return string.Format( "{1}  {0}  {1}", header, _lines );
		}

        public static string CompressXml(string xml)
        {
            string str = Regex.Replace(xml, @"(>\s*<)", @"><");
            return str;
        }

        public static string Decrypt(string value)
		{
			Cipher c = new Cipher( config.Default.PassPhrase, config.Default.SaltValue, config.Default.InitVector );
			return c.Decrypt( value );
		}

        public static string GetServerLongPath(string server, string localServerPath)
        {
            return fs.Path.GetLongPath("\\\\" + server + "\\" + localServerPath.Replace(':', '$'));
        }

        public static string GetServerLongPathWindows(string server, string localServerPath)
        {
            return "\\\\" + server + "\\" + localServerPath.Replace(':', '$');
        }

        public static string ReadFileContents(string uncPath)
        {
            return fs.File.ReadAllText(uncPath);
        }

        public static void DeleteFile(string uncPath)
        {
            fs.File.Delete(uncPath);
        }

        public static void CreateDirectory(string uncPath)
        {
            if (!fs.Directory.Exists(uncPath))
            {
                fs.Directory.CreateDirectory(uncPath);
            }
        }

        public static bool DirectoryExists(string uncPath)
        {
            return fs.Directory.Exists(uncPath);
        }

        public static string FormatNamedPowershellParameters(XmlElement parameters, String prefix, String joinedBy, bool useQuotes=true, XmlNode overrideParameters=null)
        {
            return FormatNamedParameters(parameters, prefix, joinedBy, useQuotes, overrideParameters, true);
        }
        
        public static string FormatNamedParameters(XmlElement parameters, String prefix, String joinedBy, bool useQuotes=true, XmlNode overrideParameters=null, Boolean isPowershell=false)
        {
            StringBuilder args = new StringBuilder();
            Hashtable overrideArguments = BuildNamedOverrideHash(overrideParameters);

            foreach (XmlNode node in parameters.ChildNodes)
            {
                string key = node.Name;
                string value = "";
                if (node.FirstChild != null)
                {
                    value = node.FirstChild.Value;
                    if (node.Attributes["AllowOverride"] != null)
                    {
                        if (overrideArguments.ContainsKey(key))
                        {
                            value = (string)overrideArguments[key];
                        }
                    }
                }

                if (isPowershell)
                {
                    if (value.EndsWith(@"\"))
                        value += @"\";
                }

                if (useQuotes)
                {
                    if (!(string.IsNullOrWhiteSpace(value)))
                        args.Append(prefix + key + joinedBy + @"""" + Utils.ReplaceDynamicValues(value).Replace(@"""", escQuote) + @""" ");
                    else
                        args.Append(prefix + key + joinedBy + @"""" + @""" ");
                } 
                else
                {
                    if (!(string.IsNullOrWhiteSpace(value)))
                        args.Append(prefix + key + joinedBy + Utils.ReplaceDynamicValues(value).Replace(@"""", escQuote) + @" ");
                    else
                        args.Append(prefix + key  + @" ");
                }
            }

            return args.ToString();
        }

        public static string FormatOrderedPowershellParameters(XmlElement parameters, String prefix="", bool useQuotes=true, XmlNode overrideValues = null)
        {
            return FormatOrderedParameters(parameters, prefix, useQuotes, overrideValues, true);
        }
        
        public static string FormatOrderedParameters(XmlElement parameters, String prefix="", bool useQuotes=true, XmlNode overrideValues = null, Boolean isPowershell=false)
        {
            StringBuilder args = new StringBuilder();

            Hashtable overrideArguments = BuildOrderedOverrideHash(overrideValues);

            Hashtable arguments = new Hashtable();
            int maxArg = -1;
            foreach (XmlNode node in parameters.ChildNodes)
            {
                try
                {
                    int index = GetOrderedIndex(node);
                    string value = "";
//                    if (!(node.FirstChild == null) && !(string.IsNullOrWhiteSpace(node.FirstChild.Value)))
                    if (!(node.FirstChild == null) && !(string.IsNullOrEmpty(node.FirstChild.Value)))
                        value = Utils.ReplaceDynamicValues(node.FirstChild.Value).Replace(@"""", escQuote);

                    if (overrideValues != null)
                    {
                        // Can Parameter Be Overridden
                        if (node.Attributes["AllowOverride"] != null)
                        {
                            if (overrideArguments.ContainsKey(index))
                                value = (string)overrideArguments[index];
                        }
                    }

                    if (isPowershell)
                    {
                        if (value.EndsWith(@"\"))
                            value += @"\";
                    }

                    if (index > maxArg)
                        maxArg = index;

                    if (prefix != null)
                        value = prefix + value;
                    
                    arguments.Add(index, value);
                }
                catch { }
            }

            for (int i = 0; i <= maxArg; i++)
            {
                if (useQuotes)
                {
                    if (arguments[i] == null)
                        args.Append(@""""" ");
                    else
                        args.Append(@"""" + arguments[i].ToString() + @""" ");
                }
                else
                {
                    if (arguments[i] == null)
                        args.Append(@""""" ");
                    else if (String.IsNullOrEmpty(arguments[i].ToString()))
                        args.Append(@""""" ");
                    else
                        args.Append(arguments[i].ToString() + @" ");
                }
            }

            return args.ToString();
        }

        private static int GetOrderedIndex(XmlNode node)
        {
            int index = -1;
            string name = "";
            string divider = ".";
            if (node != null)
            {
                name = node.Name;
                if (name.Contains(divider))
                    name = name.Substring(0, name.IndexOf(divider));
            }
            index = int.Parse(name.Replace('_', ' '));
            return index;
        }

        public static Hashtable BuildOrderedOverrideHash(XmlNode node)
        {
            Hashtable overrideArguments = new Hashtable();
            if (node != null)
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    int index = GetOrderedIndex(child);
                    string value = "";
                    if (!(child.FirstChild == null) && !(string.IsNullOrWhiteSpace(child.FirstChild.Value)))
                        value = Utils.ReplaceDynamicValues(child.FirstChild.Value).Replace(@"""", escQuote);
                    overrideArguments.Add(index, value);
                }
            }
            return overrideArguments;
        }

        public static Hashtable BuildNamedOverrideHash(XmlNode node)
        {
            Hashtable overrideArguments = new Hashtable();
            if (node != null)
            {
                foreach (XmlNode child in node.ChildNodes)
                {
                    string key = child.Name;
                    string value = "";
                    if (!(child.FirstChild == null) && !(string.IsNullOrWhiteSpace(child.FirstChild.Value)))
                        value = Utils.ReplaceDynamicValues(child.FirstChild.Value).Replace(@"""", escQuote);
                    overrideArguments.Add(key, value);
                }
            }
            return overrideArguments;
        }

        public static string ReplaceDynamicValues(string inStr)
        {
            string outStr = inStr;
            string pattern = null;
            Match match = null;

            // Check For "NOW" Variable, Replace With Current Time Formatted As Specified
            // Usage : "Time : ~~NOW:yyyyMMddHHmmss~~" returns "Time : 20150324114510" (March 24th, 2015 at 9:45:10 pm)
            pattern = @"~~NOW:(.*?)~~";
            if ((match = Regex.Match(outStr, pattern, RegexOptions.IgnoreCase)).Success)
            {
                String formatPattern = match.Groups[1].Value;
                outStr = Regex.Replace(outStr, pattern, DateTime.Now.ToString(formatPattern), RegexOptions.IgnoreCase);
            }

            return outStr;
        }

		#region serialize/deserialize
		//stolen from Suplex.General.XmlUtils
		public static void Serialize<T>(object data, string filePath)
		{
			XmlSerializer s = new XmlSerializer( typeof( T ) );
			XmlTextWriter w = new XmlTextWriter( filePath, Encoding.Unicode );
			w.Formatting = Formatting.Indented;
			s.Serialize( w, data );
			w.Close();
		}

		public static string Serialize<T>(object data, bool indented = true, string filePath = null, bool omitXmlDeclaration = true, bool omitXmlNamespace = true)
		{
			XmlWriterSettings settings = new XmlWriterSettings();
			settings.OmitXmlDeclaration = omitXmlDeclaration;
			settings.ConformanceLevel = ConformanceLevel.Auto;
			settings.CloseOutput = true;
			settings.Encoding = Encoding.Unicode;
			settings.Indent = indented;

			MemoryStream ms = new MemoryStream();
			XmlSerializer s = new XmlSerializer( typeof( T ) );
			XmlWriter w = XmlWriter.Create( ms, settings );
			if( omitXmlNamespace )
			{
				XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
				ns.Add( "", "" );
				s.Serialize( w, data, ns );
			}
			else
			{
				s.Serialize( w, data );
			}
			string result = Encoding.Unicode.GetString( ms.GetBuffer(), 0, (int)ms.Length );
			w.Close();

			if( !string.IsNullOrWhiteSpace( filePath ) )
			{
				using( StreamWriter file = new StreamWriter( filePath, false ) )
				{
					file.Write( result );
				}
			}

			return result;
		}

		public static T DeserializeFile<T>(string filePath)
		{
			using( FileStream fs = new FileStream( filePath, FileMode.Open, FileAccess.Read ) )
			{
				XmlSerializer s = new XmlSerializer( typeof( T ) );
				return (T)s.Deserialize( fs );
			}
		}

		public static T DeserializeString<T>(string script)
		{
			XmlSerializer s = new XmlSerializer( typeof( T ) );
			return (T)s.Deserialize( new StringReader( script ) );
		}
		#endregion
	}
}