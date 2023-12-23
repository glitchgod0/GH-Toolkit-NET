﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GH_Toolkit_Core.Debug;
using GH_Toolkit_Core.Methods;
using static GH_Toolkit_Core.PAK.PAK;
using static GH_Toolkit_Core.QB.QB;
using static GH_Toolkit_Core.QB.QBConstants;

namespace GH_Toolkit_Core.PAK
{
    public class PAK
    {
        [DebuggerDisplay("Entry: {FullName}")]
        public class PakEntry
        {
            public string? Extension { get; set; }
            public uint StartOffset { get; set; }
            public uint FileSize { get; set; }
            public string? AssetContext { get; set; }
            public string? FullName { get; set; }
            public string? NameNoExt { get; set; }
            public uint Parent { get; set; }
            public int Flags { get; set; }
            public string? FullFlagPath { get; set; } // If flags contains 0x20 byte, this gets added
            public byte[]? EntryData { get; set; }
            public byte[]? ExtraData { get; set; }
            public string ConsoleType {  get; set; }
            public int ByteLength { get; set; }
            public PakEntry()
            {

            }
            public PakEntry(string console, string game)
            {
                if (console == CONSOLE_PS2 && game == GAME_GH3)
                {
                    MakeLastEntry("last");
                }
                else
                {
                    MakeLastEntry(".last");
                }
            }
            public PakEntry(byte[] bytes, string console)
            {
                EntryData = bytes;
                FileSize = (uint)EntryData.Length;
                ConsoleType = console;
                ByteLength = 32;
            }
            public void MakeLastEntry(string lastType)
            {
                EntryData = [0xAB, 0xAB, 0xAB, 0xAB];
                FileSize = (uint)EntryData.Length;
                ByteLength = 32;
                SetExtension(lastType);
                SetNameNoExt("0x0");
                SetFullName("0x0");
            }
            public void SetExtension(string extension)
            {
                Extension = extension;
            }
            public void SetNameNoExt(string nameNoExt)
            {
                NameNoExt = nameNoExt;
            }
            public void SetFullFlagPath(string fullFlagPath)
            {
                FullFlagPath = fullFlagPath;
            }
            public void SetFullName(string fullName)
            { 
                FullName = fullName;
            }
            public void SetNames(bool isQb)
            {
                if (ConsoleType == CONSOLE_PS2)
                {
                    if ((Flags & 0x20) != 0)
                    {
                        AssetContext = FullFlagPath;
                        FullFlagPath = FullFlagPath.PadRight(160, '\0');
                        ByteLength += 160;
                    }
                    if (!isQb && FullFlagPath.EndsWith(DOTPS2))
                    {
                        AssetContext = AssetContext.Substring(0, AssetContext.Length - 4);
                    }
                    FullName = FLAGBYTE;
                }
                else
                {

                }
            }
            public void SetFlags()
            {
                if (ConsoleType == CONSOLE_PS2)
                {
                    switch (Extension)
                    {
                        case DOT_QB:
                        case DOT_MQB:
                        case DOT_SQB:
                            Flags |= 0x20;
                            break;
                        default: 
                            throw new NotImplementedException();
                    }
                    if (NameNoExt.LastIndexOf(_SFX) != -1)
                    {
                        Flags |= 0x02;
                    }
                    else if (NameNoExt.LastIndexOf(_GFX) != -1)
                    {
                        Flags |= 0x04;
                    }
                }
                else
                {
                    Flags = 0;
                }
            }
        }

        public static void ProcessPAKFromFile(string file)
        {
            string fileName = Path.GetFileName(file);
            if (fileName.IndexOf(".pab", 0, fileName.Length, StringComparison.CurrentCultureIgnoreCase) != -1)
            {
                return;
            }
            if (fileName.IndexOf(".pak", 0, fileName.Length, StringComparison.CurrentCultureIgnoreCase) == -1)
            {
                throw new Exception("Invalid File");
            }
            string fileNoExt = fileName.Substring(0, fileName.IndexOf(".pak"));
            string fileExt = Path.GetExtension(file);
            Console.WriteLine($"Extracting {fileNoExt}");
            string folderPath = Path.GetDirectoryName(file);
            string NewFolderPath = Path.Combine(folderPath, fileNoExt);
            string songCheck = "_song";
            string songName = "";
            List<PakEntry> pakEntries;
            bool debugFile = fileName.Contains("dbg.pak");
            string masterFilePath = Path.Combine(NewFolderPath, "master.txt");
            if (fileName.Contains(songCheck))
            {
                songName = fileName.Substring(0, fileName.IndexOf(songCheck));
            }

            byte[] test_pak = File.ReadAllBytes(file);
            byte[] test_pab = null;

            // Check for a corresponding .pab file
            string pabFilePath = Path.Combine(folderPath, fileNoExt + $".pab{fileExt}");
            if (File.Exists(pabFilePath))
            {
                test_pab = File.ReadAllBytes(pabFilePath);
            }

            string endian;
            if (fileExt == ".ps2")
            {
                endian = "little";
            }
            else
            {
                endian = "big";
                fileExt = ".xen";
            }
            try
            {
                pakEntries = ExtractPAK(test_pak, test_pab, endian: endian, songName: songName);
            }
            catch (Exception ex)
            {
                test_pak = Compression.DecompressData(test_pak);
                if (test_pab != null)
                {
                    test_pab = Compression.DecompressData(test_pab);
                }
                pakEntries = ExtractPAK(test_pak, test_pab, endian: endian, songName: songName);
            }

            foreach (PakEntry entry in pakEntries)
            {
                string pakFileName = entry.FullName;
                if (!pakFileName.EndsWith(fileExt, StringComparison.CurrentCultureIgnoreCase))
                {
                    pakFileName += fileExt;
                }

                string saveName = Path.Combine(NewFolderPath, pakFileName);
                Console.WriteLine(pakFileName);
                Directory.CreateDirectory(Path.GetDirectoryName(saveName));
                File.WriteAllBytes(saveName, entry.EntryData);

                if (debugFile)
                {
                    Console.WriteLine($"Writing {pakFileName}");
                    string[] lines = File.ReadAllLines(saveName);

                    using (StreamWriter masterFileWriter = File.AppendText(masterFilePath))
                    {
                        foreach (string line in lines)
                        {
                            if (line.StartsWith("0x"))
                            {
                                masterFileWriter.WriteLine(line);
                            }
                        }
                    }
                }

            }
        }
        private static uint CheckPabType(byte[] pakBytes, string endian = "big")
        {
            bool flipBytes = ReadWrite.FlipCheck(endian);
            byte[] pabCheck = new byte[4];
            Array.Copy(pakBytes, 4, pabCheck, 0, 4);
            if (flipBytes)
            {
                Array.Reverse(pabCheck);
            }
            uint pabOff = BitConverter.ToUInt32(pabCheck);
            if (pabOff == 0)
            {
                return 0;
            }
            return pabOff;
        }
        public static List<PakEntry> ExtractPAK(byte[] pakBytes, byte[]? pabBytes, string endian = "big", string songName = "")
        {
            ReadWrite reader = new ReadWrite(endian);
            if (Compression.isChnkCompressed(pakBytes))
            {
                pakBytes = Compression.DecompressWTPak(pakBytes);
            }
            if (pabBytes != null)
            {
                uint pabType = CheckPabType(pakBytes, endian);
                switch (pabType)
                {
                    case 0:
                        throw new Exception("PAK type not yet implemented.");
                    case uint size when size >= pakBytes.Length:
                        byte[] bytes = new byte[pabType + pabBytes.Length];
                        Array.Copy(pakBytes, 0, bytes, 0, pakBytes.Length);
                        Array.Copy(pabBytes, 0, bytes, pabType, pabBytes.Length);
                        pakBytes = bytes;
                        break;
                }
            }
            List<PakEntry> pakList = ExtractOldPak(pakBytes, endian, songName);

            return pakList;
        }
        public static List<PakEntry> ExtractOldPak(byte[] pakBytes, string endian, string songName = "")
        {
            ReadWrite reader = new ReadWrite(endian);
            MemoryStream stream = new MemoryStream(pakBytes);
            List<PakEntry> PakList = new List<PakEntry>();
            Dictionary<uint, string> headers = DebugReader.MakeDictFromName(songName);

            bool TryGH3 = false;
            while (true)
            {
                PakEntry entry = new PakEntry();
                uint header_start = (uint)stream.Position; // To keep track of which entry since the offset in the header needs to be added to the StartOffset below

                uint extension = reader.ReadUInt32(stream);
                if (extension != 0x2cb3ef3b && extension != 0xb524565f)
                {
                    entry.Extension = DebugReader.DebugCheck(headers, extension);
                }
                else
                {
                    break;
                }
                if (!entry.Extension.StartsWith("."))
                {
                    entry.Extension = "." + entry.Extension;
                }
                uint offset = reader.ReadUInt32(stream);
                entry.StartOffset = offset + header_start;
                uint filesize = reader.ReadUInt32(stream);
                entry.FileSize = filesize;
                uint asset = reader.ReadUInt32(stream);
                entry.AssetContext = DebugReader.DebugCheck(headers, asset);
                uint fullname = reader.ReadUInt32(stream);
                entry.FullName = DebugReader.DebugCheck(headers, fullname);
                uint name = reader.ReadUInt32(stream);
                entry.NameNoExt = DebugReader.DebugCheck(headers, name);
                if (entry.FullName.StartsWith("0x"))
                {
                    entry.FullName = $"{entry.FullName}.{entry.NameNoExt}";
                }
                uint parent = reader.ReadUInt32(stream);
                entry.Parent = parent;
                int flags = reader.ReadInt32(stream);
                entry.Flags = flags;
                if ((flags & 0x20) != 0)
                {
                    var skipTo = stream.Position + 160;
                    string tempString = ReadWrite.ReadUntilNullByte(stream);
                    switch (tempString)
                    {
                        case string s when s.StartsWith("ones\\"):
                            tempString = "z" + tempString;
                            break;
                        case string s when s.StartsWith("cripts\\"):
                            tempString = "s" + tempString;
                            break;
                        case string s when s.StartsWith("kies\\"):
                            tempString = "s" + tempString;
                            break;
                        case string s when s.StartsWith("ongs\\"):
                            tempString = "s" + tempString;
                            break;
                        case string s when s.StartsWith("odels\\"):
                            tempString = "m" + tempString;
                            break;
                        case string s when s.StartsWith("ak\\"):
                            tempString = "p" + tempString;
                            break;
                    }
                    entry.FullName = tempString;
                    stream.Position = skipTo;
                }
                try
                {
                    entry.EntryData = new byte[entry.FileSize];
                    Array.Copy(pakBytes, entry.StartOffset, entry.EntryData, 0, entry.FileSize);
                    if (entry.FullName == "0x00000000.0x00000000")
                    {
                        entry.FullName = entry.AssetContext;
                    }
                    // entry.FullName = entry.FullName.Replace(".qb", entry.Extension);
                    if (entry.FullName.IndexOf(entry.Extension, StringComparison.CurrentCultureIgnoreCase) == -1) 
                    {
                        entry.FullName += entry.Extension;
                    }
                    PakList.Add(entry);
                }
                catch (Exception ex)
                {
                    if (TryGH3 == true)
                    {
                        Console.WriteLine(ex.Message);
                        throw new Exception("Could not extract PAK file.");
                    }
                    Console.WriteLine("Could not find last entry. Trying Guitar Hero 3 Compression.");
                    PakList.Clear();
                    pakBytes = Compression.DecompressData(pakBytes);
                    stream = new MemoryStream(pakBytes);
                    TryGH3 = true;
                }
            }

            Console.WriteLine("Success!");
            return PakList;
        }
        public class PakCompiler
        {
            public string Game { get; set; }
            public string ConsoleType { get; set; }
            public bool IsQb {  get; set; }
            public bool Split {  get; set; }
            private ReadWrite Writer { get; set; }
            public PakCompiler(string game, bool isQb = false, bool split = false)
            {
                Game = game;
                IsQb = isQb; // Meaning qb.pak, really only used for PS2 to differentiate .qb files from .mqb files
                Split = split;
            }
            private void SetConsole(string filePath)
            {
                string ext = Path.GetExtension(filePath).ToLower();
                if (ext == DOTPS2)
                {
                    ConsoleType = CONSOLE_PS2;
                    Writer = new ReadWrite("little");
                }
                else
                {
                    ConsoleType = CONSOLE_XBOX;
                    Writer = new ReadWrite("big");
                }
                Console.WriteLine($"Compiling {ConsoleType} PAK file.");
            }
            public (byte[]? itemData, byte[]? otherData, string console) CompilePAK(string folderPath)
            {
                if (!Directory.Exists(folderPath))
                {
                    throw new NotSupportedException("Argument given is not a folder.");
                }

                //string[] entries = Directory.GetFileSystemEntries(folderPath);
                string[] entries = Directory.GetFileSystemEntries(folderPath, "*", SearchOption.AllDirectories);
                /*string[] entries = File.ReadAllLines("D:\\Visual Studio\\Repos\\PAK Compiler\\qb_listing.txt");

                for (int i  = 0; i < entries.Length; i++)
                {
                    entries[i] = Path.Combine(folderPath, entries[i]);
                }*/
                /*
                 * List<string> files = new List<string>();
                foreach(string entry in entries)
                {
                    if (File.Exists(entry))
                    {
                        files.Add(entry);
                    }
                    else if (Directory.Exists(entry))
                    {
                        string[] tempEntries = Directory.GetFileSystemEntries(entry, "*", SearchOption.AllDirectories);
                        List<string> tempFiles = new List<string>();
                        foreach (string tempEntry in tempEntries)
                        {
                            if (File.Exists(tempEntry))
                            {
                                tempFiles.Add(tempEntry.Substring(0, tempEntry.IndexOf('.')));
                            }
                        }
                        tempEntries = tempFiles.ToArray();
                        Array.Sort(tempEntries);
                        Array.Reverse(tempEntries);
                        files.AddRange(tempEntries);
                    }
                }

                entries = files.ToArray(); */

                for (int i = 0; i < entries.Length; i++)
                {
                    if (File.Exists(entries[i]))
                    {
                        SetConsole(entries[i]);
                        break;
                    } 
                }
                List<PakEntry> PakEntries = new List<PakEntry>();
                List<string> fileNames = new List<string>();
                int pakSize = 16;

                foreach (string entry in entries)
                {
                    if (File.Exists(entry))
                    {
                        byte[] fileData;
                        string relPath = GetRelPath(folderPath, entry);
                        if (Path.GetExtension(entry) == DOT_Q)
                        {
                            List<QBItem> qBItems = ParseQFile(entry);
                            relPath += "b";
                            fileData = CompileQbFile(qBItems, relPath);
                            if (ConsoleType == CONSOLE_PS2)
                            {
                                relPath += ".ps2";
                            }
                            else
                            {
                                relPath += ".xen";
                            }
                        }
                        else
                        {
                            fileData = File.ReadAllBytes(entry);
                        }
                        PakEntry pakEntry = new PakEntry(fileData, ConsoleType);
                        
                        pakEntry.SetFullFlagPath(relPath);
                        pakEntry.SetNameNoExt(GetFileNoExt(Path.GetFileName(relPath)));
                        pakEntry.SetExtension(GetFileExt(relPath));
                        pakEntry.SetFlags();
                        pakEntry.SetNames(IsQb);

                        if (!fileNames.Contains(pakEntry.NameNoExt))
                        {
                            fileNames.Add(pakEntry.NameNoExt);
                        }
                        else
                        {

                        }
                        PakEntries.Add(pakEntry);
                        pakSize += pakEntry.ByteLength;
                    }
                }

                PakEntries.Add(new PakEntry(ConsoleType, Game));
                pakSize += PakEntries[fileNames.Count].ByteLength;

                byte[] pakData;
                byte[] pabData;

                using (MemoryStream pak = new MemoryStream())
                using (MemoryStream pab = new MemoryStream())
                {
                    int bytesPassed = 0;
                    foreach (PakEntry entry in PakEntries)
                    {
                        entry.StartOffset = (uint)(pakSize - bytesPassed + pab.Position);
                        pak.Write(Writer.ValueHex(entry.Extension), 0, 4);
                        pak.Write(Writer.ValueHex(entry.StartOffset), 0, 4);
                        pak.Write(Writer.ValueHex(entry.FileSize), 0, 4);
                        pak.Write(Writer.ValueHex(entry.AssetContext), 0, 4);
                        pak.Write(Writer.ValueHex(entry.FullName), 0, 4);
                        pak.Write(Writer.ValueHex(entry.NameNoExt), 0, 4);
                        pak.Write(Writer.ValueHex(entry.Parent), 0, 4);
                        pak.Write(Writer.ValueHex(entry.Flags), 0, 4);
                        if ((entry.Flags & 0x20) != 0)
                        {
                            ReadWrite.WriteStringBytes(pak, entry.FullFlagPath);
                        }
                        pab.Write(entry.EntryData);
                        Writer.PadStreamTo(pab, 16);
                        bytesPassed += entry.ByteLength;
                    }
                    ReadWrite.WriteStringBytes(pak, "\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0\0");
                    pakData = pak.ToArray();
                    pabData = pab.ToArray();
                }
                return (pakData, pabData, ConsoleType);

            }
            private string GetFileExt(string path)
            {
                string fileEnd = path.Substring(path.LastIndexOf('.')).ToLower();
                string extension;
                switch (fileEnd)
                {
                    case DOTNGC:
                    case DOTPS2:
                    case DOTPS3:
                    case DOTXEN:
                        extension = Path.GetExtension(Path.GetFileNameWithoutExtension(path)).ToLower();
                        break;
                    default:
                        extension = Path.GetExtension(path).ToLower();
                        break;
                }
                if (ConsoleType == CONSOLE_PS2 && extension == DOT_QB && !IsQb)
                {
                    if (path.IndexOf("_scripts") != -1 && path.IndexOf("song_scripts") == -1)
                    {
                        extension = DOT_SQB;
                    }
                    else
                    {
                        extension = DOT_MQB;
                    }
                }
                return extension;
            }
            private string GetFileNoExt(string path)
            {
                string noExt = Path.GetFileNameWithoutExtension(path);
                while (Path.GetFileNameWithoutExtension(noExt) != noExt)
                {
                    noExt = Path.GetFileNameWithoutExtension(noExt);
                }
                return noExt;
            }
            private string GetRelPath(string folderPath, string entry)
            {
                string relPath = Path.GetRelativePath(folderPath, entry);
                if (ConsoleType == CONSOLE_PS2)
                {
                    return relPath;
                }

                return Path.GetFileNameWithoutExtension(relPath); 
            }
        }
    }
}
