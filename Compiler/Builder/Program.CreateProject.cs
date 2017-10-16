using Bridge.Contract;
using Bridge.Translator;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Bridge.Builder
{
    public partial class Program
    {
        private static void CreateProject(ILogger logger, BridgeOptions bridgeOptions, string folder, string template)
        {
            var rootPath = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            var templatesPath = Path.Combine(rootPath, "Templates");
            var templatePath = Path.Combine(templatesPath, template);

            if(Directory.Exists(templatePath))
            {
                foreach (string dirPath in Directory.GetDirectories(templatePath, "*", SearchOption.AllDirectories))
                {
                    var path = dirPath.Replace(templatePath, folder);

                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }                    
                }                    

                foreach (string newPath in Directory.GetFiles(templatePath, "*.*", SearchOption.AllDirectories))
                {
                    File.Copy(newPath, newPath.Replace(templatePath, folder), true);
                }                    

                var packagesConfigPath = Path.Combine(folder, "packages.config");
                if (File.Exists(packagesConfigPath))
                {
                    XDocument config = XDocument.Load(packagesConfigPath);
                    var packages = config
                        .Element("packages")
                        .Elements("package")
                        .Select(packageElem => new { id = packageElem.Attribute("id").Value, version = packageElem.Attribute("version")?.Value })
                        .ToList();

                    foreach (var pkg in packages)
                    {
                        AddPackage(logger, bridgeOptions, folder, pkg.id, pkg.version);
                    }

                    File.Delete(packagesConfigPath);
                }
            }            
        }

        private static void AddPackage(ILogger logger, BridgeOptions bridgeOptions, string folder, string packageName, string version = null)
        {
            var packagesFolder = Path.Combine(folder, "packages");
            if(!Directory.Exists(packagesFolder))
            {
                Directory.CreateDirectory(packagesFolder);
            }

            WebClient client = new WebClient();
            bool hasVersion = !string.IsNullOrWhiteSpace(version);
            string uri = "https://www.nuget.org/api/v2/package/" + packageName + (hasVersion ? "/" + version : "");
            string name = packageName + (hasVersion ? "." + version : "");
            string localFile = Path.Combine(packagesFolder, name + ".nupkg");

            if(File.Exists(localFile))
            {
                File.Delete(localFile);
            }

            Console.WriteLine();
            Console.Write($"Installing {packageName}: ");

            using (var spinner = new ConsoleSpinner())
            {                
                spinner.Start();
                client.DownloadFile(uri, localFile);

                if (!String.IsNullOrEmpty(client.ResponseHeaders["Content-Disposition"]))
                {
                    var fileName = client.ResponseHeaders["Content-Disposition"].Substring(client.ResponseHeaders["Content-Disposition"].IndexOf("filename=") + 9).Replace("\"", "");
                    name = Path.GetFileNameWithoutExtension(fileName);
                }
            }

            Console.Write("Done.");
            Console.WriteLine();

            var packageFolder = Path.Combine(packagesFolder, name);
            if (Directory.Exists(packageFolder))
            {
                Directory.Delete(packageFolder, true);
            }

            Directory.CreateDirectory(packageFolder);
            ZipFile.ExtractToDirectory(localFile, packageFolder);
        }
    }
}
