﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.IO;

namespace bin2mp4
{
    public partial class Form1 : Form
    {
        string[] args = Environment.GetCommandLineArgs();
        public byte[] inBIN;
        public byte[] outMP4 = new Byte[_MP4.ArraySize];
        public static string outMP4_name;
        public static string outMP4_dir;
        public static string targetVer = "540";
        public static int injectOffset = 4424;
        public static bool cmdConvert = false;

        //Attach to console so we can output info during command line usage
        [DllImport("kernel32.dll")]
        static extern bool AttachConsole(int dwProcessId);
        private const int ATTACH_PARENT_PROCESS = -1;

        public Form1()
        {
            InitializeComponent();
            AttachConsole(ATTACH_PARENT_PROCESS);

            //Check if command line arguments were given and do those instead of launching Windows form
            if (args.Length > 1)
            { 
                _OpenFile(_CommandLine.Init(args, targetVersionBox.Items));
                Environment.Exit(0);
            }
            targetVersionBox.SelectedIndex = 1; //Set default version to 5.4
        }
        public void _OpenFile(string fileName)
        {
            inBIN = File.ReadAllBytes(fileName);
            if (!cmdConvert)
            { 
                outMP4_name = Path.GetFileNameWithoutExtension(fileName);
            }

            if (inBIN.Length > 29832)
            {
                label1.Text = "Input file exceeds 29.1KB (29832 bytes) size limit!";
                if (cmdConvert)
                {
                    Console.WriteLine("");
                    Console.WriteLine(" Input file exceeds 29.1KB (29832 bytes) size limit!");
                    Environment.Exit(-5);
                }
            }
            else
            {
                _GenerateMP4(targetVer);
                _Save2File();
            }
        }

        public void _GenerateMP4(string inVer)
        {
            _WriteBytes(_MP4.headerBytes, 0);
            _FillBytes(_MP4.Header_Pattern.offset, _MP4.Header_Pattern.length, _MP4.Header_Pattern.spacing, _MP4.Header_Pattern.value);
            _WriteBytes(_MP4.BinLeadInBytes, _MP4.BinLeadInOffset);
            _FillBytes(_MP4.Bin_Fill.offset, _MP4.Bin_Fill.length, _MP4.Bin_Fill.spacing, _MP4.Bin_Fill.value);

            //Start of version specific code
            if (inVer == "532" || inVer == "540")
            {
                _PatternBytes(_MP4.v532_Pattern.offset, _MP4.v532_Pattern.length, _MP4.v532_Pattern.spacing, _MP4.v532_Pattern.value); 
                _WriteBytes(_MP4.v532_Footer, _MP4.v532_FooterOffset);
                if(inVer == "532") //Patch header for 5.3.2
                {
                    for(int i = 0; i <_MP4.v532_Header_Patch.offsets.Length; i++)
                    {
                        outMP4[ _MP4.v532_Header_Patch.offsets[i] ] = _MP4.v532_Header_Patch.values[i];
                    }
                }
            }
            else if (inVer == "550" || inVer == "551")
            {
                _PatternBytes(_MP4.v550_Pattern.offset, _MP4.v550_Pattern.length, _MP4.v550_Pattern.spacing, _MP4.v550_Pattern.value);
                _WriteBytes(_MP4.v550_Footer, _MP4.v550_FooterOffset); 
            }

            _FillBytes(_MP4.Footer_Fill.offset, _MP4.Footer_Fill.length, _MP4.Footer_Fill.spacing, _MP4.Footer_Fill.value);
            _WriteBytes(_MP4.Footer_Magic, _MP4.Footer_Magic_Offset);
           // _PatternBytes(_MP4.Footer_Pattern.offset, _MP4.Footer_Pattern.length, _MP4.Footer_Pattern.spacing, _MP4.Footer_Pattern.values);

            _WriteBytes(inBIN, _MP4.BinLeadInOffset + _MP4.BinLeadInBytes.Length - 2); //Write our input .bin file to the required position in the file
            _WriteBinSize(); //Write the hex length of our bin file to the appropriate location
        }

        private void _WriteBytes(byte[] inBytes, int offset)
        {
            for (int i = 0; i < inBytes.Length; i++)
            {
                outMP4[i + offset] = inBytes[i];
            }
        }

        private void _WriteBinSize()
        {
            byte[] inBIN_size = BitConverter.GetBytes(inBIN.Length);
            for (int i = 0; i < 4; i++)
            {
                outMP4[_MP4.BinLeadInOffset + _MP4.BinLeadInBytes.Length - 3 - i] = inBIN_size[i];
            }
        }

        private void _FillBytes(int offset, int size, int spacing, byte byteVal)
        {
            for (int i = 0; i < size; i += spacing)
            {
                outMP4[i + offset] = byteVal;
            }
        }
        private void _PatternBytes(int offset, int size, int spacing, byte[] byteVal)
        {
            int byteChoice = 0;
            for (int i = 0; i < size; i += spacing)
            {
                outMP4[i + offset] = byteVal[byteChoice];
                byteChoice += 1;
                if(byteChoice == byteVal.Length)
                {
                    byteChoice = 0;
                }
            }
        }

        public void _Save2File()
        {
            if (!cmdConvert)
            {
                using (SaveFileDialog saveFileDialog1 = new SaveFileDialog())
                {
                    saveFileDialog1.FileName = outMP4_name;
                    saveFileDialog1.Filter = "MP4 File | *.mp4";
                    if (DialogResult.OK != saveFileDialog1.ShowDialog())
                    {
                        return;
                    }
                    File.WriteAllBytes(saveFileDialog1.FileName, outMP4);
                }
            }
            else
            {
                string tempDir = outMP4_dir + "\\" + outMP4_name + ".mp4";
                File.WriteAllBytes(tempDir, outMP4);
                Console.WriteLine("");
                Console.WriteLine(" File saved to: \"" + tempDir + "\"");
            }
        }
//Windows Forms event triggers, further info about these can be found in Form1.Designer.cs
        private void button1_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog1 = new OpenFileDialog())
            {
                openFileDialog1.Filter = "Binary File | *.bin";

                if (DialogResult.OK != openFileDialog1.ShowDialog())
                {
                    return;
                }
                _OpenFile(openFileDialog1.FileName);
            }
        }

        private void dropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
        }

        private void dropZone_DragDrop(object sender, DragEventArgs e)
        {
            int binsFound = 0;
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            for (int i = 0; i < files.Length; i++ )
            {
                if(Path.GetExtension(files[i]) == ".bin")
                {
                    _OpenFile(files[i]);
                    binsFound += 1;
                }
            }
            if(binsFound == 0)
            {
                label1.Text = "No .bin files found!";
            }
            else if(binsFound == 1)
            {
                label1.Text = "1 .bin file converted.";
            }
            else
            {
                label1.Text = binsFound.ToString() + " .bin files converted.";
            }
        }

        private void targetVersionBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            targetVer = targetVersionBox.Text.Replace(".","");
            Console.WriteLine(targetVer);
        }

        private void versionLabel_MouseClick(object sender, MouseEventArgs e)
        {
            Process.Start("http://gbatemp.net/threads/tool-bin2mp4.417414/");
        }
    
        private void versionLabel_MouseEnter(object sender, EventArgs e)
        {
            versionLabel.ForeColor = label2.ForeColor;
            Console.WriteLine("hovered over");
        }

        private void versionLabel_MouseLeave(object sender, EventArgs e)
        {
            versionLabel.ForeColor = System.Drawing.Color.SlateGray;
        }
    }
}
