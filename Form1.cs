using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Speech.AudioFormat;
using System.Speech.Synthesis;
using System.Threading;
using System.Windows.Forms;

namespace RegionalRadio
{
    public partial class MainFrm : Form
    {
        #region...变量定义...
        //下位机接收报警端口 (可配置)
        static string AlarmRecevPort = "12349";
        //存储已发送指令，单位发送完成的主机信息
        static Dictionary<string, string[]> htSendMsg = new Dictionary<string, string[]>();
        FileStream fileStrem;
        SpeechSynthesizer synth = new SpeechSynthesizer();
        UdpClient uc = new UdpClient();
        Thread ThKZ;
        Thread ThGH;
        #endregion

        public MainFrm()
        {
            InitializeComponent();
        }

        private void MainFrm_Load(object sender, EventArgs e)
        {
            try
            {
                ReLoad();
                //下位机接收报警信息端口
                if (ConfigurationManager.AppSettings.AllKeys.Contains("SendPort_Alarm"))
                {
                    AlarmRecevPort = ConfigurationManager.AppSettings["SendPort_Alarm"];
                }
                //刷新主机信息
                ThGH = new Thread(new ThreadStart(GetHostInfoAsync));
                ThGH.Start();
                ThKZ = new Thread(new ThreadStart(Radio));
                ThKZ.Start();
            }
            catch (Exception ex)
            {
                LogHelper.WriteDebugLog("MainFrm_Load调用异常： " + ex.Message + "\r\n");
            }
        }

        /// <summary>
        /// 获取当前ip
        /// </summary>
        /// <returns></returns>
        public string GetLocalIp()
        {
            ///获取本地的IP地址
            string AddressIP = string.Empty;
            foreach (IPAddress _IPAddress in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (_IPAddress.AddressFamily.ToString() == "InterNetwork")
                {
                    AddressIP = _IPAddress.ToString();
                    return AddressIP;
                }
            }
            return AddressIP;
        }

        private void GetHostInfoAsync()
        {
            try
            {
                while (true)
                {
                    ReLoad();
                    string str = string.Format(@"select * from BroadCast");
                    DataTable dt = DbHelperSQLUp.ExecuteDataTable(str);
                    string s;
                    for (int i = 0; i < dt.Rows.Count; i++)
                    {
                        s = dt.Rows[i]["RegionCode"].ToString();
                        string[] strSendMsg = new string[5];
                        strSendMsg[0] = dt.Rows[i]["IP"].ToString();
                        strSendMsg[1] = dt.Rows[i]["Duration"].ToString();
                        strSendMsg[2] = dt.Rows[i]["CycleTime"].ToString();
                        strSendMsg[3] = DateTime.Now.ToString();
                        strSendMsg[4] = "open";
                        if (!htSendMsg.ContainsKey(s))
                        {
                            htSendMsg.Add(s, strSendMsg);
                        }
                        else
                        {
                            htSendMsg[s][0] = strSendMsg[0];
                            htSendMsg[s][1] = strSendMsg[1];
                            htSendMsg[s][2] = strSendMsg[2];
                        }
                    }
                    Thread.Sleep(60 * 1000);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteDebugLog("GetHostInfoAsync调用异常： " + ex.Message + "\r\n");
            }
        }

        private delegate void DataSource();
        private void ReLoad()
        {
            try
            {
                string str = string.Format(@"insert into BroadCast(RegionId,RegionCode,RegionName) select RegionId,RegionCode,RegionName from Po_Region where RegionDelete=0 and RegionId not in (select RegionId from BroadCast)");
                DbHelperSQLUp.ExecuteSql(str);
                str = string.Format(@"select RegionCode as 区域编号,RegionName as 区域名称,Duration as 持续时间,CycleTime as 循环时间,IP as 绑定主机 from BroadCast");
                DataTable dt = DbHelperSQLUp.ExecuteDataTable(str);
                if (this.dataGridView1.InvokeRequired == false)
                {
                    dataGridView1.DataSource = dt;
                }
                else
                {
                    DataSource DMSGD = new DataSource(ReLoad);
                    this.dataGridView1.Invoke(DMSGD);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteDebugLog("ReLoad调用异常： " + ex.Message + "\r\n");
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string RegionCode = dataGridView1.SelectedRows[0].Cells[0].Value.ToString();
            Edit edit = new Edit(RegionCode);
            edit.ShowDialog();
            ReLoad();
        }

        private void Radio()
        {
            try
            {
                while (true)
                {
                    string str = string.Format(@"select RegionName,RegionCode,QYRS from
	                                                (select RegionId,Sum(num)AS QYRS from
		                                                (select RegionId,BaseStationName,(select count(*) from
			                                                Ac_ActualTime with(nolock)
			                                                inner join Po_Card with(nolock) on Ac_ActualTime.CardCode=Po_Card.CardCode
			                                                inner join Po_Employee on Po_Employee.EmployeeCardId=Po_Card.CardId
			                                                where Po_Employee.IsDelete=0 and Ac_ActualTime.StationMark=1 and Ac_ActualTime.StationCode=Cast(po_basestation.BaseStationCode as int))as num
		                                                from Po_BaseStation right join po_region on po_basestation.basestationregion=po_region.regionid
		                                                where po_basestation.IsDelete=0) as T
	                                                group by RegionId) as K
                                                left join Po_Region on Po_Region.RegionId=K.RegionId");
                    DataTable dt = DbHelperSQLUp.ExecuteDataTable(str);
                    byte[] SendMsg = new byte[1024];
                    foreach (var item in htSendMsg)
                    {
                        TimeSpan ts = DateTime.Now - Convert.ToDateTime(item.Value[3]);
                        int j = ts.Minutes;
                        if (item.Value[4] == "open")
                        {
                            if (!string.IsNullOrEmpty(item.Value[0]) && j >= Convert.ToInt32(item.Value[2]))
                            {
                                DataRow[] dr = dt.Select("RegionCode='" + item.Key + "'");
                                string Content = dr[0]["RegionName"].ToString() + "现有" + dr[0]["QYRS"].ToString() + "人";
                                synth.SetOutputToWaveFile("warning.wav", new SpeechAudioFormatInfo(8000, AudioBitsPerSample.Sixteen, AudioChannel.Mono));
                                synth.Speak(Content);
                                synth.SetOutputToNull();
                                string[] ip = item.Value[0].Split(',');
                                for (int i = 0; i < ip.Length; i++)
                                {
                                    SendFile("warning.wav", ip[i]);
                                }
                                htSendMsg[item.Key][4] = "close";
                                htSendMsg[item.Key][3] = DateTime.Now.ToString();
                            }
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(item.Value[0]) && j >= Convert.ToInt32(item.Value[1]))
                            {
                                SendMsg[0] = 0XE6;
                                SendMsg[1] = 0XE6;
                                string[] ip = item.Value[0].Split(',');
                                for (int i = 0; i < ip.Length; i++)
                                {
                                    uc.Send(SendMsg, 1024, ip[i], 12349);
                                }
                                htSendMsg[item.Key][4] = "open";
                                htSendMsg[item.Key][3] = DateTime.Now.ToString();
                            }
                        }
                    }
                    Thread.Sleep(1 * 1000);
                }
            }
            catch (Exception ex)
            {
                LogHelper.WriteDebugLog("Radio调用异常： " + ex.Message + "\r\n");
            }
        }

        /// <summary>
        /// 发送语音文件到指定主机
        /// </summary>
        /// <param name="file"></param>
        /// <param name="IP"></param>
        private void SendFile(string file, string IP)
        {
            UdpClient clientSend = new UdpClient();
            fileStrem = new FileStream(file, FileMode.Open);
            try
            {
                byte[] RvMessageW;
                int ReceiveCount = 1;
                do
                {
                    if (ReceiveCount > 8)
                        return;
                    IPEndPoint romoteIP = new IPEndPoint(IPAddress.Parse(IP), 12349); //获取基站IP
                    clientSend.Connect(romoteIP);
                    int fileReadSize = 0;
                    long fileLength = 0;
                    int bz = 0;
                    while (fileLength < fileStrem.Length)
                    {
                        byte[] buffer = new byte[2048];
                        if (bz == 0)
                        {
                            byte[] s = System.Text.Encoding.UTF8.GetBytes(file);
                            byte[] sname = new byte[s.Length + 7];
                            sname[0] = 0XE4;
                            sname[1] = 0XE4;
                            byte[] intBuff = BitConverter.GetBytes(s.Length);
                            sname[2] = intBuff[0];
                            s.CopyTo(sname, 3);
                            byte aa = sname[1];
                            byte[] SongBuff = new byte[4];
                            byte[] SongBuff1 = BitConverter.GetBytes(fileStrem.Length);
                            for (int i = 0; i < 4; i++)
                            {
                                SongBuff[i] = SongBuff1[i];
                            }
                            SongBuff.CopyTo(sname, s.Length + 3);
                            byte[] RvMessageT;
                            int SaveCount = 1;
                            do
                            {
                                if (SaveCount > 8)
                                    return;
                                clientSend.Send(sname, sname.Length);
                                clientSend.Client.ReceiveTimeout = 5000;
                                RvMessageT = clientSend.Receive(ref romoteIP);
                                SaveCount += 1;
                            }
                            while (RvMessageT[RvMessageT.Length - 1] == 0XAA);
                            bz = 1;
                        }
                        else
                        {
                            buffer[0] = 0XE5;
                            buffer[1] = 0XE5;
                            fileReadSize = fileStrem.Read(buffer, 2, buffer.Length - 2);
                            if (fileReadSize < 2046)
                            {
                                byte[] sendByte = new byte[fileReadSize + 2];
                                for (int i = 0; i < sendByte.Length; i++)
                                    sendByte[i] = buffer[i];
                                clientSend.Send(sendByte, sendByte.Length);
                            }
                            else
                            {
                                clientSend.Send(buffer, buffer.Length);
                            }
                            fileLength += fileReadSize;
                        }
                        Thread.Sleep(5);
                    }
                    fileStrem.Flush();
                    fileStrem.Close();

                    RvMessageW = clientSend.Receive(ref romoteIP);
                    ReceiveCount += 1;
                }
                while (RvMessageW[RvMessageW.Length - 1] == 0XAA);
                clientSend.Client.Close();
                clientSend.Close();
            }
            catch (Exception ex)
            {
                fileStrem.Flush();
                fileStrem.Close();
                clientSend.Client.Close();
                clientSend.Close();
                LogHelper.WriteDebugLog("SendFile调用异常： " + ex.Message + "\r\n");
            }
        }

        private void MainFrm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                this.notifyIcon1.ShowBalloonTip(1000, "系统提示", "请勿关闭！", ToolTipIcon.Warning);
                e.Cancel = true;
                this.ShowInTaskbar = false;
                this.notifyIcon1.Icon = this.Icon;
                this.Hide();
            }
        }

        private void notifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                contextMenuStrip1.Show();
            }

            if (e.Button == MouseButtons.Left)
            {
                this.Visible = true;
                this.WindowState = FormWindowState.Normal;
            }
        }

        private void 退出toolStripMenuItem_Click(object sender, EventArgs e)
        {
            DialogResult rt = MessageBox.Show("确定要退出服务程序吗？", "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (rt == DialogResult.Yes)
            {
                byte[] SendMsg = new byte[1024];
                SendMsg[0] = 0XE6;
                SendMsg[1] = 0XE6;
                foreach (var item in htSendMsg)
                {
                    string[] ip = item.Value[0].Split(',');
                    for (int i = 0; i < ip.Length; i++)
                    {
                        uc.Send(SendMsg, 1024, ip[i], 12349);
                    }
                }
                Application.ExitThread();
                Process.GetCurrentProcess().Kill();
            }
        }
    }
}
