using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PQLauncher
{
    public class ModPackage
    {
        public class PackageEntry
        {
            public string name;
            public string path;
            public bool directory;
            public string md5;
            public IDictionary<string, PackageEntry> children;

            public PackageEntry(string name, string path, bool directory)
            {
                this.name = name;
                this.path = path;
                this.directory = directory;
                this.md5 = null;
                if (directory)
                {
                    children = new Dictionary<String, PackageEntry>();
                }
                else
                {
                    children = null;
                }
            }
        }

        PackageEntry m_filesRoot;
        string m_name;
        Uri m_address;

        public string Name { get => m_name; set => m_name = value; }
        public Uri Address { get => m_address; set => m_address = value; }

        public ModPackage(string name, Uri address)
        {
            Name = name;
            m_filesRoot = new PackageEntry("", "", true);
            m_address = address;
        }

        /// <summary>
        /// Get all the files in the package (not directories)
        /// </summary>
        /// <param name="root">Root entry to search from</param>
        /// <returns>A list of all files</returns>
        public IEnumerable<PackageEntry> GetFiles(PackageEntry root = null)
        {
            if (root == null)
            {
                root = m_filesRoot;
            }

            foreach (PackageEntry entry in root.children.Values)
            {
                if (entry.directory)
                {
                    //Recursively get all the children from here
                    foreach (PackageEntry child in GetFiles(entry))
                    {
                        yield return child;
                    }
                }
                else
                {
                    //It's a file, get it
                    yield return entry;
                }
            }
        }

        public bool AddFile(string path, string md5)
        {
            //Scan through the file tree to find the directory of the file we're adding
            PackageEntry root = m_filesRoot;

            string[] components = path.Split('/');

            //Ignore leading /
            for (int i = 1; i < components.Length - 1; i ++)
            {
                string component = components[i];
                if (root.children.ContainsKey(component))
                {
                    root = root.children[component];
                }
                else
                {
                    PackageEntry directory = new PackageEntry(component, root.path + "/" + component, true);
                    root.children.Add(component, directory);
                    root = directory;
                }
            }
            //Root should be the dir of the added file
            string fileName = components.Last();
            if (root.children.ContainsKey(fileName))
            {
                //Already have this one!
                return false;
            }
            //Add the new file
            PackageEntry file = new PackageEntry(fileName, root.path + "/" + fileName, false);
            file.md5 = md5;
            root.children.Add(fileName, file);

            return true;
        }
    }
}
