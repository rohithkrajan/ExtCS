using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;

namespace ExtCS.Debugger.ListCommandHelper
{
    public class XmlDataHelper:DataHelper
    {
        string _path;
        public XmlDocument  _xmlDoc;
        
        public override bool SaveData(ICommand command)
        {
            try
            {
                
                _xmlDoc = new XmlDocument();
                if (File.Exists(_path))
                {
                    _xmlDoc.Load(_path);
                }
                else
                {
                    _xmlDoc.LoadXml(@"<commands><recentList></recentList><recentFolders></recentFolders></commands>");
                }
                string hash = command.Args.GetHashString();
                XmlNode commandNode = _xmlDoc.SelectSingleNode("/commands/recentList/command[@hash='" + hash + "']");
                if (commandNode == null)
                {
                    var node = (XmlElement)_xmlDoc.CreateNode(XmlNodeType.Element, "command", "");

                    node.SetAttribute("hash", hash);
                    node.SetAttribute("commandText", ProcessData(command));
                    node.SetAttribute("scriptName", command.ScriptName);
                    var commands = _xmlDoc.SelectSingleNode("/commands/recentList");
                    if (commands.HasChildNodes)
                    {
                        commands.InsertBefore(node, commands.ChildNodes[0]);
                    }
                    else
                        commands.AppendChild(node);

                    _xmlDoc.Save(_path);
                    return true;

                }
                return true;

            }
            catch (Exception)
            {

                throw;
            }
            

        }


        public override IList<ICommand> GetRecentCommands()
        {
            throw new NotImplementedException();
        }

        public override IList<ICommand> GetContainers()
        {
            throw new NotImplementedException();
        }

        public override IList<ICommand> GetScriptsFromContainer(string containername)
        {
            throw new NotImplementedException();
        }

        public override IList<ICommand> Search(string searchString, string containerName)
        {
            throw new NotImplementedException();
        }

        public override IList<ICommand> Search(string searchString)
        {
            throw new NotImplementedException();
        }
    }
}
