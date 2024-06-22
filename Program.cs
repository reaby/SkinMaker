﻿using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Diagnostics;
using System.Configuration;
using System.Net;
using System.Net.Http;
using Assimp;
using System.Xml;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Please provide a skin name.");
            Console.Write("Press any key to close..."); Console.ReadLine();
            Environment.Exit(0);
        }

        // string Skin_Name = args[0];
        string SkinFbxPath = args[0];
        if(!File.Exists(SkinFbxPath)){
            Console.WriteLine($"File {Path.GetFileName(SkinFbxPath)} doesn't exists.");
            Console.Write("Press any key to close..."); Console.ReadLine();
            Environment.Exit(0);
        }
        if(!SkinFbxPath.Contains("\\Work\\")){
            Console.WriteLine("File isn't in a Work folder.");
            Console.Write("Press any key to close..."); Console.ReadLine();
            Environment.Exit(0);
        }
        string Skin_Name = Path.GetFileNameWithoutExtension(SkinFbxPath);
        string Skin_Name_Ext = Path.GetFileName(SkinFbxPath);
        string Skin_Directory = Path.GetDirectoryName(SkinFbxPath) ?? "";
        if(String.IsNullOrEmpty(Skin_Directory)){
            Console.WriteLine("Unhandled error.");
            Console.Write("Press any key to close..."); Console.ReadLine();
            Environment.Exit(0);
        }

        bool fakeshad = false;
        if(args.Length > 1 && args[1].ToLower() == "--fakeshad"){
            fakeshad = true;
        }else{
            Console.Write("Do you want enable the FakeShad.dds? (y/n) > ");
            string cki = Console.ReadLine() ?? "";
            if (cki.ToString().ToLower() == "y")
                fakeshad = true;
        }
        
        string TM_Install_Path = ConfigurationManager.AppSettings["TM_Install_Path"] ?? "";
        string TM_User_Path = ConfigurationManager.AppSettings["TM_User_Path"] ?? "";

        if(String.IsNullOrEmpty(TM_Install_Path)){
            Console.WriteLine("Please specify a TM_Install_Path variable in the app.config");
            Console.Write("Press any key to close..."); Console.ReadLine();
            Environment.Exit(0);
        }
        
        if(String.IsNullOrEmpty(TM_User_Path)){
            Console.WriteLine("Please specify a TM_User_Path variable in the app.config");
            Console.Write("Press any key to close..."); Console.ReadLine();
            Environment.Exit(0);
        }

        Console.WriteLine("\nGenerating " + Skin_Name + ".MesParams.xml based on " + Skin_Name_Ext + "...");
        GenerateMeshParams(SkinFbxPath, Skin_Directory, Skin_Name);
        Console.WriteLine(Skin_Name + ".MeshParams.xml generation OK.");

        string currentFolder = AppDomain.CurrentDomain.BaseDirectory;
        if(!File.Exists(Path.Combine(currentFolder, "skinfix.exe"))){
            Console.WriteLine("\nskinfix.exe not found. Attempting to download...");
            DownloadSkinFix(Path.Combine(currentFolder, "skinfix.exe")).GetAwaiter().GetResult();
            Console.WriteLine("skinfix.exe downloaded. Continuing.");
        }
        string Converter_Exe_Path = Path.Combine(currentFolder, "skinfix.exe");

        if (!File.Exists(Path.Combine(Skin_Directory, Skin_Name + ".MeshParams.xml")))
        {
            Console.WriteLine(Skin_Name + ".MeshParams.xml doesn't exist.");
            Console.Write("Press any key to close..."); Console.ReadLine();
            Environment.Exit(0);
        }

        Console.WriteLine("\nStarting NadeoImporter process...");

        int index = SkinFbxPath.IndexOf("Work") + "Work".Length;
        string skinRelativePath = SkinFbxPath.Substring(index);
        int lastSlashIndex = skinRelativePath.LastIndexOf("\\");
        skinRelativePath = skinRelativePath.Substring(0, lastSlashIndex);

        string nadeoImporterOutput = StartProcess(Path.Combine(TM_Install_Path, "NadeoImporter.exe"), "Mesh " + Path.Combine(skinRelativePath, Skin_Name_Ext));
        if(!nadeoImporterOutput.Split('\n').Reverse().Skip(1).First().StartsWith("Created :user:") && !nadeoImporterOutput.Split('\n').Reverse().Skip(1).First().EndsWith(".Mesh.gbx")){
            Console.WriteLine(nadeoImporterOutput);
            Console.WriteLine("NadeoImporter failed, check the output above.");
            Console.Write("Press any key to close..."); Console.ReadLine();
            Environment.Exit(0);
        }else{
            Console.WriteLine("NadeoImporter process OK.");
        }
        // Process.Start(Path.Combine(TM_Install_Path, "NadeoImporter.exe"), "Mesh " + "Skins\\Models\\" + Skin_Name + "\\" + Skin_Name + ".fbx").WaitForExit();

        if (fakeshad)
        {
            Console.WriteLine("\nStarting skinfix process with FakeShad...");
            StartProcess(Converter_Exe_Path, Path.Combine(TM_User_Path, "Skins", "Models", Skin_Name, Skin_Name + ".Mesh.gbx") + " --fakeshad");
            // Process.Start(Converter_Exe_Path, Path.Combine(TM_User_Path, "Skins", "Models", Skin_Name, Skin_Name + ".Mesh.gbx") + " --fakeshad").WaitForExit();
        }
        else
        {
            Console.WriteLine("\nStarting skinfix process...");
            StartProcess(Converter_Exe_Path, Path.Combine(TM_User_Path, "Skins", "Models", Skin_Name, Skin_Name + ".Mesh.gbx"));
            // Process.Start(Converter_Exe_Path, Path.Combine(TM_User_Path, "Skins", "Models", Skin_Name, Skin_Name + ".Mesh.gbx")).WaitForExit();
        }
        Console.WriteLine("skinfix process OK...");

        Console.WriteLine("\nZipping files...");
        Console.WriteLine(ZIPFiles(SkinFbxPath, Skin_Directory, Skin_Name));

        Console.WriteLine("\nSkin created successfully!");
        Console.Write("Press any key to close..."); Console.ReadLine();
    }

    static void GenerateMeshParams(string filePath, string Skin_Directory, string Skin_Name)
    {
        using (var context = new AssimpContext())
        {
            // var scene = context.ImportFile(Path.Combine(TM_User_Path, "Work", "Skins", "Models", Skin_Name, Skin_Name + ".fbx"));
            var scene = context.ImportFile(filePath);
            var fbxMaterials = scene.Materials;
            XmlDocument doc = new XmlDocument();

            // Create the root element and add it to the document
            XmlElement root = doc.CreateElement("MeshParams");
            doc.AppendChild(root);

            // Set the attributes of the root element
            root.SetAttribute("MeshType", "Vehicle");
            root.SetAttribute("SkelSocketPrefix", "_");

            // Create the Materials element and add it to the root element
            XmlElement materials = doc.CreateElement("Materials");
            root.AppendChild(materials);
            
            for (int i = 0; i < fbxMaterials.Count; i++)
            {
                XmlElement material = doc.CreateElement("Material");
                string currMat = fbxMaterials[i].Name;
                material.SetAttribute("Name", currMat);

                if(currMat.StartsWith("SkinDmg_"))
                    material.SetAttribute("Model", "SkinDmg");
                else if(currMat.StartsWith("DetailsDmgNormal_"))
                    material.SetAttribute("Model", "DetailsDmgNormal");
                else if(currMat.StartsWith("GlassDmgCrack_"))
                    material.SetAttribute("Model", "GlassDmgCrack");
                else if(currMat.StartsWith("GlassDmgDecal_"))
                    material.SetAttribute("Model", "GlassDmgDecal");
                else if(currMat.StartsWith("DetailsDmgDecal_"))
                    material.SetAttribute("Model", "DetailsDmgDecal");
                else if(currMat.StartsWith("SkinDmgDecal_"))
                    material.SetAttribute("Model", "SkinDmgDecal");
                else if(currMat.StartsWith("Gems_"))
                    material.SetAttribute("Model", "Gems");
                else if(currMat.StartsWith("GlassRefract_"))
                    material.SetAttribute("Model", "GlassRefract");
                else
                    material.SetAttribute("Model", "DetailsDmgNormal");
                
                materials.AppendChild(material);
            }

            // Create the Constants, UvAnims, VisibleIds, and Color elements and add them to the root element
            root.AppendChild(doc.CreateElement("Constants"));
            root.AppendChild(doc.CreateElement("UvAnims"));
            root.AppendChild(doc.CreateElement("VisibleIds"));
            root.AppendChild(doc.CreateElement("Color"));

            // Save the XML document to a file
            // doc.Save(Path.Combine(TM_User_Path, "Work", "Skins", "Models", Skin_Name, Skin_Name + ".MeshParams.xml"));
            doc.Save(Path.Combine(Skin_Directory, Skin_Name + ".MeshParams.xml"));
        }
    }

    static async Task DownloadSkinFix(string path)
    {
        using (HttpClient client = new HttpClient())
        {
            // Download the file as a Stream
            using (Stream fileStream = await client.GetStreamAsync(@"https://openplanet.dev/file/119/download"))
            {
                // Copy the contents of the Stream to a file
                using (FileStream outputFileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await fileStream.CopyToAsync(outputFileStream);
                }
            }
        }
    }

    static string StartProcess(string fileName, string arguments){
        string processOutput = "";
        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,  // Example command to get .NET Core version
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = new Process())
        {
            process.StartInfo = startInfo;

            // Start the process
            process.Start();

            // Begin asynchronous read of standard output and error
            process.StandardInput.Flush();
            process.StandardInput.Close();

            processOutput = process.StandardOutput.ReadToEnd();
            // Wait for the process to complete
            process.WaitForExit();

            // Display exit code and completion message
            // Console.WriteLine($"Process exited with code {process.ExitCode}");
        }

        return processOutput;
    }

    static string ZIPFiles(string filePath, string Skin_Directory, string Skin_Name){
        string[] fileExtensions = { "MainBody.Mesh.Gbx" };
        string folderWork = Path.Combine(Skin_Directory);
        string folderSkin = Path.Combine(Skin_Directory.Replace("Work\\", ""));
        var filesWork = Directory.GetFiles(folderWork);
        var filesSkin = Directory.GetFiles(folderSkin);
        var filesToZip = filesWork.Where(file =>
            !Path.GetFileName(file).ToLower().Contains("meshparams") &&
            !Path.GetFileName(file).ToLower().EndsWith(".fbx") &&
            Path.GetFileName(file).ToLower() != (Skin_Name + ".Mesh.Gbx").ToLower()
        ).ToList();

        // Add MainBody.Mesh.gbx to list of files
        filesToZip.AddRange(filesSkin.Where(file => Path.GetFileName(file).ToLower() != (Skin_Name + ".Mesh.Gbx").ToLower()).ToList());

        if (filesToZip.Any())
        {
            string zipFileName = Path.Combine(folderWork, Skin_Name + ".zip");

            using (ZipArchive archive = ZipFile.Open(zipFileName, ZipArchiveMode.Create))
            {
                foreach (var file in filesToZip)
                {
                    archive.CreateEntryFromFile(file, Path.GetFileName(file));
                }
            }

            int index = filePath.IndexOf("Work");
            string trackmaniaDocumentRootPath = filePath.Substring(0, index - 1);
            string destinationFolder = Path.Combine(trackmaniaDocumentRootPath, "Skins", "Models", "CarSport");
            if (!Directory.Exists(destinationFolder))
            {
                Directory.CreateDirectory(destinationFolder);
                Console.WriteLine("No CarSport folder found. Please create it.");
                Console.Write("Press any key to close..."); Console.ReadLine();
                Environment.Exit(0);
            }

            File.Move(zipFileName, Path.Combine(destinationFolder, Skin_Name + ".zip"), true);
            return "Created zip file: " + Path.Combine(destinationFolder, Skin_Name + ".zip");
        }
        else
        {
            return "No files found in " + folderWork;
        }
    }
}