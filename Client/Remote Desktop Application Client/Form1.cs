using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using Microsoft.AspNet.SignalR.Client;
using System.IO;
using Newtonsoft.Json;

namespace Remote_Desktop_Application_Client
{

    public partial class Form1 : Form
    {
        static int totalBytesRead = 0;
        Rectangle bounds = Screen.PrimaryScreen.Bounds;
        Size size;
        public int height;
        public int width;
        static HubConnection conn;
        public IHubProxy hub;
        public string userID;
        static IConnect conObj;
        public bool connection_established = true;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            string url = @"http://172.20.10.2:8080";
            string hubName = "BroadCastHub";

            size = bounds.Size;
            height = bounds.Height;
            width = bounds.Width;
            userID = textBox1.Text;

            conObj = new Connect(this);
            conObj.Connection(url, hubName);
            conn = conObj.conn;
            hub = conObj.hub;

            conObj.HubOnBool("StartBroadcast"); 
                      
            textBox2.Text = conn.ConnectionId;
        }

        public void CaptureScreen()
        {
            while (connection_established)
            {
                using (Bitmap bitmap = new Bitmap(width, height))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(new Point(bounds.Left, bounds.Top), Point.Empty, size);
                    }
                    
                    Bitmap r_sized = new Bitmap(bitmap, new Size(width, height));
                    MemoryStream stream = new MemoryStream();
                    r_sized.Save(stream, ImageFormat.Jpeg);
                    Byte[] bytes = stream.ToArray();
                    if (conn.State == Microsoft.AspNet.SignalR.Client.ConnectionState.Connected)
                        hub.Invoke<string>("SendFrames", bytes);
    
                    Thread.Sleep(300);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Thread t1 = new Thread(EstablishConnection);

            if (textBox1.Text != "")
            {
                t1.Start();
                t1.Join();
                string targetID = textBox1.Text;
                this.Height = height;
                this.Width = width;
                panel1.Visible = true;
                connection_established = true;
                conObj.HubOnByte("ReceiveFrame");
            }
            else
            {
                MessageBox.Show("Lütfen ID giriniz");
            }
        }

        void EstablishConnection()
        {
            userID = textBox1.Text;
            hub.On<ReturnResult>("EstablishConnection", (param1) =>
            {
                if (param1.Result == true)  Console.WriteLine("Established");
                else MessageBox.Show(param1.Detail);

            });
            try
            {
                hub.Invoke<string>("ConnectionStart", userID).Wait();
            }
            catch(Exception e)
            {

            }
        }

        private void but_geri_Click(object sender, EventArgs e)
        {
            panel1.Visible = false;
            connection_established = false;
            hub.Invoke<string>("StopBroadcastReceiving", userID).Wait();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            connection_established = false;
            conn.Dispose();
            
            Application.ExitThread();
        }


        private void button1_Click_2(object sender, EventArgs e)
        {
            string path = "";
            OpenFileDialog file = new OpenFileDialog();
            file.InitialDirectory = "C:\\";
            if(file.ShowDialog() == DialogResult.OK)
            {
                path = file.FileName;
            }
            string[] pathName = path.Split('.');
            List<Byte[]> completed_file = SplitFile(path);
            

            for (int i = 0; i < completed_file.Count(); i++)
            {
                   hub.Invoke<string>("SendFile", completed_file[0], userID,completed_file.Count(), totalBytesRead, pathName[1]).Wait();

            }
            completed_file.Clear();
        }   

        static List<Byte[]> SplitFile(string path)
        {
            int chunkSize = 150000;
            totalBytesRead = 0;
            List<byte[]> completed_file = new List<byte[]>();
            byte[] buffer = new byte[chunkSize];
            string fileName = path;
            using (Stream input = File.OpenRead(fileName))
            {
                int index = 0;
                while (input.Position < input.Length)
                {
                    int chunkBytesRead = 0;
                    while (chunkBytesRead < chunkSize)
                    {
                        int bytesRead = input.Read(buffer,
                                                   chunkBytesRead,
                                                   chunkSize - chunkBytesRead);

                        if (bytesRead == 0)
                        {
                            break;
                        }

                        chunkBytesRead += bytesRead;
                    }
                    completed_file.Add(buffer);
                    totalBytesRead += chunkBytesRead;
                    index++;
                    buffer = new Byte[chunkSize];
                }
            }
            return completed_file;

        }
    }

    interface IConnect
    {
        HubConnection conn { get; set; }
        IHubProxy hub { get; set; }
        bool connection_established { get; set; }
        void Connection(string url, string hubName);
        void HubOnBool(string methodName);
        void HubOnByte(string methodName);
    }

    class Connect : IConnect
    {
        public HubConnection conn { get; set; }
        public IHubProxy hub { get; set; }
        public bool connection_established { get; set; }
        Form1 f_;

        public Connect(Form1 f)
        {
            f_ = f;
        }

        public void Connection(string url, string hubName)
        {
            conn = new HubConnection(url);
            hub = conn.CreateHubProxy(hubName);
            conn.Start().ContinueWith(t =>
            {
                if (t.IsFaulted)
                    Console.WriteLine(t.Exception.GetBaseException());
                else
                    Console.WriteLine("Connected to Hub");
            }).Wait();
        }

        public void HubOnBool(string methodName)
        {
            int sayac = 0;
            List<Byte[]> completed = new List<byte[]>();
            hub.On<byte[], int, int, string>("ReceiveFile", (file_byte, size, totalBytesRead, extension) =>
            {
                
                if (completed.Count < size)
                {
                    completed.Add(file_byte);
                }
                if (completed.Count == size)
                {
                   
                    using (Stream output = File.Create(@"C:\" + "\\" + sayac+"100" + "."+ extension))
                    {
                        List<Byte> file = Merge_Bytes(completed);
                        output.Write(file.ToArray(), 0, totalBytesRead);
                        sayac++;
                    }
                }
            });

            hub.On<bool>("StartBroadcast", (param1) =>
            {
                if (param1 == true)
                {
                    MessageBox.Show("Broadcast has started");
                    connection_established = true;
                    Thread t1 = new Thread(f_.CaptureScreen);

                    t1.Start();
                }
                else
                {
                    connection_established = false;
                    MessageBox.Show("Broadcast has stopped");
                }
            });
        }

        static List<Byte> Merge_Bytes(List<Byte[]> completed_file)
        {
            List<Byte> comp = new List<Byte>();
            for (int i = 0; i < completed_file.Count(); i++)
            {
                for (int j = 0; j < completed_file[i].Count(); j++)
                {
                    comp.Add(completed_file[i][j]);
                }
            }
            return comp;
        }

        public void HubOnByte(string methodName)
        {
            hub.On<byte[]>(methodName, (image_byte) =>
            {
                var ms = new MemoryStream(image_byte);
                Image image = Image.FromStream(ms);
                Bitmap r_sized = new Bitmap(image, new Size(f_.width, f_.height));
                MemoryStream stream = new MemoryStream();
                r_sized.Save(stream, ImageFormat.Png);
                f_.pictureBox1.Image = r_sized;
            });
        }
    }
}
