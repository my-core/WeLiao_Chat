using MyControls;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace WeLiao_Client
{
    public partial class Main : SkinForm
    {
        /// <summary>
        /// 服务器IP地址IP
        /// </summary>
        private string ServerIP;
        /// <summary>
        /// 监听端口
        /// </summary>
        private int Port;
        /// <summary>
        /// 退出标识
        /// </summary>
        private bool IsExit = false;
        /// <summary>
        /// 侦听TCP网络客户端连接对象
        /// </summary>
        private TcpClient _TcpClient;
        /// <summary>
        /// 二进制读流
        /// </summary>
        private BinaryReader _BinaryReader;
        /// <summary>
        /// 二进制写流
        /// </summary>
        private BinaryWriter _BinaryWriter;

        /// <summary>
        /// 构造
        /// </summary>
        public Main()
        {
            InitializeComponent();
            Random r = new Random((int)DateTime.Now.Ticks);
            this.textBox_UserName.Text = "User" + r.Next(100, 999);
            listBox_UserList.HorizontalScrollbar = true;
            SetServerIPAndPort();
        }
        /// <summary>
        /// 登录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnLogin_Click(object sender, EventArgs e)
        {
            btnLogin.Enabled = false;
            try
            {
                //此处为方便演示，实际使用时要将Dns.GetHostName()改为服务器域名
                //IPAddress ipAd = IPAddress.Parse("182.150.193.7");
                _TcpClient = new TcpClient();
                _TcpClient.Connect(IPAddress.Parse(ServerIP), Port);
                AddChatsToListBox("连接成功");
                this.btnSend.Enabled = true;
            }
            catch (Exception ex)
            {
                AddChatsToListBox("连接失败，原因：" + ex.Message);
                btnLogin.Enabled = true;
                return;
            }
            //获取网络流
            NetworkStream networkStream = _TcpClient.GetStream();
            //将网络流作为二进制读写对象
            _BinaryReader = new BinaryReader(networkStream);
            _BinaryWriter = new BinaryWriter(networkStream);
            SendMessage("Login," + textBox_UserName.Text);
            Thread threadReceive = new Thread(new ThreadStart(ReceiveData));
            threadReceive.IsBackground = true;
            threadReceive.Start();
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (listBox_UserList.SelectedIndex != -1)
            {
                SendMessage("Talk," + listBox_UserList.SelectedItem + "," + this.textBox_Content.Text + "\r\n");
                textBox_Content.Clear();
            }
            else
            {
                MessageBox.Show("请先在【当前在线】中选择一个对话着");
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            //未与服务器连接前 client 为 null
            if (_TcpClient != null)
            {
                try
                {
                    SendMessage("Logout," + this.textBox_UserName.Text);
                    IsExit = true;
                    this._BinaryReader.Close();
                    this._BinaryWriter.Close();
                    _TcpClient.Close();
                }
                catch
                {
                }
            }
        }

        #region 方法
        /// <summary>
        /// 设置IP地址及端口
        /// </summary>
        private void SetServerIPAndPort()
        {
            try
            {
                ServerIP = ConfigurationManager.AppSettings["ServerIP"].ToString(); //设定IP
                Port = int.Parse(ConfigurationManager.AppSettings["ServerPort"].ToString()); //设定端口
            }
            catch (Exception ex)
            {
                SaveLog("设置IP地址及端口异常", ex.Message);
            }
        }
        /// <summary>
        /// 向服务端发送消息
        /// </summary>
        /// <param name="message"></param>
        private void SendMessage(string message)
        {
            try
            {
                //将字符串写入网络流，此方法会自动附加字符串长度前缀
                _BinaryWriter.Write(message);
                _BinaryWriter.Flush();
            }
            catch
            {
                AddChatsToListBox("发送失败");
            }
        }
        private delegate void AddItemToListBoxDelegate(string str);
        /// <summary>
        /// 在listBox_Chats消息记录中追加信息
        /// </summary>
        /// <param name="str">要追加的信息</param>
        private void AddChatsToListBox(string str)
        {
            if (listBox_Chats.InvokeRequired)
            {
                AddItemToListBoxDelegate d = AddChatsToListBox;
                listBox_Chats.Invoke(d, str);
            }
            else
            {
                listBox_Chats.Items.Add(str);
                listBox_Chats.SelectedIndex = listBox_Chats.Items.Count - 1;
                listBox_Chats.ClearSelected();
            }
        }
        /// <summary>
        /// 处理接收的客户端信息
        /// </summary>
        /// <param name="userState">客户端信息</param>
        private void ReceiveData()
        {
            string receiveString = null;
            while (IsExit == false)
            {
                try
                {
                    //从网络流中读出字符串，此方法会自动判断字符串长度前缀
                    receiveString = _BinaryReader.ReadString();
                }
                catch (Exception)
                {
                    if (IsExit == false)
                    {
                        AddChatsToListBox("与服务器断开连接……");
                    }
                    break;
                }
                string[] splitString = receiveString.Split(',');
                string command = splitString[0].ToLower();
                switch (command)
                {
                    case "login"://登录消息
                        AddOnline(splitString[1]);
                        break;
                    case "logout"://退出消息
                        RemoveUserByName(splitString[1]);
                        return;
                    case "talk"://聊天消息
                        AddChatsToListBox(splitString[1] + "：\r\n");
                        AddChatsToListBox(receiveString.Substring(splitString[0].Length + splitString[1].Length + 2));
                        break;
                    default:
                        AddChatsToListBox("什么意思啊：" + receiveString);
                        break;
                }
            }
            Application.Exit();
        }
        private delegate void AddOnlineDelegate(string message);
        /// <summary>
        /// 在在线框（lst_Online)中添加其他客户端信息
        /// </summary>
        /// <param name="userName"></param>
        private void AddOnline(string userName)
        {
            if (this.listBox_UserList.InvokeRequired)
            {
                AddOnlineDelegate d = new AddOnlineDelegate(AddOnline);
                listBox_UserList.Invoke(d, new object[] { userName });
            }
            else
            {
                listBox_UserList.Items.Add(userName);
                listBox_UserList.SelectedIndex = listBox_UserList.Items.Count - 1;
                listBox_UserList.ClearSelected();
            }
        }
        private delegate void RemoveUserNameDelegate(string userName);
        /// <summary>
        /// 移除指定Name的下线的用户
        /// </summary>
        /// <param name="userName"></param>
        private void RemoveUserByName(string userName)
        {
            if (listBox_UserList.InvokeRequired)
            {
                RemoveUserNameDelegate d = new RemoveUserNameDelegate(RemoveUserByName);
                listBox_UserList.Invoke(d, userName);
            }
            else
            {
                listBox_UserList.Items.Remove(userName);
                listBox_UserList.SelectedIndex = listBox_UserList.Items.Count - 1;
                listBox_UserList.ClearSelected();
            }
        }
        #endregion

        /// <summary>
        /// 写日志
        /// </summary>
        /// <param name="strTitle"></param>
        /// <param name="strContent"></param>
        public void SaveLog(string strTitle, string strContent)
        {
            try
            {
                string Path = AppDomain.CurrentDomain.BaseDirectory + "Log/" + DateTime.Now.Year + "/" + DateTime.Now.Month + "/";
                string FilePath = Path + DateTime.Now.Day + "_Log.txt";
                if (!Directory.Exists(Path)) Directory.CreateDirectory(Path);
                if (!File.Exists(FilePath))
                {
                    FileStream FsCreate = new FileStream(FilePath, FileMode.Create);
                    FsCreate.Close();
                    FsCreate.Dispose();
                }
                FileStream FsWrite = new FileStream(FilePath, FileMode.Append, FileAccess.Write);
                StreamWriter SwWrite = new StreamWriter(FsWrite);
                SwWrite.WriteLine(string.Format("{0}{1}[{2}]{3}", "--------------------------------", strTitle, DateTime.Now.ToString("HH:mm"), "--------------------------------"));
                SwWrite.Write(strContent);
                SwWrite.WriteLine("\r\n");
                SwWrite.WriteLine(" ");
                SwWrite.Flush();
                SwWrite.Close();
            }
            catch { }
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }

        private void Main_Load(object sender, EventArgs e)
        {

        }

       
    }
}
