using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Motivation;
using System.Net.Sockets;
using Newtonsoft.Json;
using System.Diagnostics;

namespace SP_MP4_client
{
    public class MessagePage:MyTabPage
    {
        class Manager
        {
            Socket socket=null;
            public void Connect(string ip,int port)
            {
                lock (syncRoot)
                {
                    if (socket != null) socket.Dispose();
                    socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    socket.Connect(ip, port);
                    socket.Blocking = false;
                }
            }
            public void Disconnect()
            {
                lock (syncRoot)
                {
                    socket.Dispose();
                    socket = null;
                }
            }
            public object syncRoot = new object();
            public void Send(string msg)
            {
                lock (syncRoot)
                {
                    if (socket == null) return;
                    socket.Send(Encoding.UTF8.GetBytes(msg + "\n"));
                }
            }
            public string Receive()
            {
                lock (syncRoot)
                {
                    if (socket == null) return null;
                    try
                    {
                        string ans = "";
                        while (true)
                        {
                            byte[] buf = new byte[1];
                            if (socket.Receive(buf) == 0) return null;
                            char c = (char)buf[0];
                            if (c == '\n') break;
                            ans += c;
                        }
                        return ans;
                    }
                    catch(System.Net.Sockets.SocketException error)
                    {
                        if (error.Message == "A non-blocking socket operation could not be completed immediately") return null;
                        else throw error;
                    }
                }
            }
        }
        public enum StateEnum { Disconnected, Idle, Matching, Chatting };
        StateEnum State = StateEnum.Disconnected;
        bool firstIdle = false;
        void SetState(StateEnum state)
        {
            if(State==StateEnum.Chatting&&state!=StateEnum.Chatting)
            {
                TXBchat.AppendText("==========對話已結束==========\r\n");
            }
            else if(State != StateEnum.Chatting && state == StateEnum.Chatting)
            {
                TXBchat.AppendText("現在開始聊天吧！\r\n");
            }
            State = state;
            switch(state)
            {
                case StateEnum.Disconnected:
                    {
                        var text = new string[3] { "/c (連線至伺服器)", "/t (沒有作用)", "/q (沒有作用)" };
                        var enabled = new bool[3] { true, false, false };
                        BTNconnect.Text = text[0];
                        BTNmatch.Text = text[1];
                        BTNquit.Text = text[2];
                        BTNconnect.Enabled = enabled[0];
                        BTNmatch.Enabled = enabled[1];
                        BTNquit.Enabled = enabled[2];
                    }
                    break;
                case StateEnum.Idle:
                    {
                        if (!firstIdle) firstIdle = true;
                        else Msg($"回到閒置狀態");
                        var text = new string[3] { "/c (結束網路連線)", "/t (嘗試匹配)", "/q (沒有作用)" };
                        var enabled = new bool[3] { true, true, false };
                        BTNconnect.Text = text[0];
                        BTNmatch.Text = text[1];
                        BTNquit.Text = text[2];
                        BTNconnect.Enabled = enabled[0];
                        BTNmatch.Enabled = enabled[1];
                        BTNquit.Enabled = enabled[2];
                    }
                    break;
                case StateEnum.Matching:
                    {
                        var text = new string[3] { "/c (結束網路連線)", "/t (沒有作用)", "/q (放棄匹配)" };
                        var enabled = new bool[3] { true, false, true };
                        BTNconnect.Text = text[0];
                        BTNmatch.Text = text[1];
                        BTNquit.Text = text[2];
                        BTNconnect.Enabled = enabled[0];
                        BTNmatch.Enabled = enabled[1];
                        BTNquit.Enabled = enabled[2];
                    }
                    break;
                case StateEnum.Chatting:
                    {
                        var text = new string[3] { "/c (結束網路連線)", "/t (沒有作用)", "/q (結束聊天)" };
                        var enabled = new bool[3] { true, false, true };
                        BTNconnect.Text = text[0];
                        BTNmatch.Text = text[1];
                        BTNquit.Text = text[2];
                        BTNconnect.Enabled = enabled[0];
                        BTNmatch.Enabled = enabled[1];
                        BTNquit.Enabled = enabled[2];
                    }
                    break;
            }
        }
        Manager manager = new Manager();
        MyTableLayoutPanel TLPmain;
        MyTextBox TXBmsg, TXBinput,TXBchat;
        MyButton BTNconnect, BTNmatch, BTNquit;
        InfoPage info;
        LogPage log;
        public MessagePage(InfoPage _info,LogPage _log):base("主頁")
        {
            info = _info;
            log = _log;
            {
                TLPmain = new MyTableLayoutPanel(3, 3, "PPP", "PAA");
                {
                    TXBchat = new MyTextBox(true);
                    TLPmain.Controls.Add(TXBchat, 0, 0);
                    TLPmain.SetColumnSpan(TXBchat, 2);
                }
                {
                    TXBmsg = new MyTextBox(true);
                    TLPmain.Controls.Add(TXBmsg, 2, 0);

                }
                {
                    TXBinput = new MyTextBox(false);
                    //TXBinput.AcceptsReturn = true;
                    TXBinput.Multiline = true;
                    TXBinput.Height = 50;
                    TXBinput.TextChanged += TXBinput_TextChanged;
                    TLPmain.Controls.Add(TXBinput, 0, 1);
                    TLPmain.SetColumnSpan(TXBinput, 3);
                }
                {
                    BTNconnect = new MyButton("");
                    BTNconnect.Click += ButtonClicked;
                    TLPmain.Controls.Add(BTNconnect, 0, 2);
                }
                {
                    BTNmatch = new MyButton("");
                    BTNmatch.Click += ButtonClicked;
                    TLPmain.Controls.Add(BTNmatch, 1, 2);
                }
                {
                    BTNquit = new MyButton("");
                    BTNquit.Click += ButtonClicked;
                    TLPmain.Controls.Add(BTNquit, 2, 2);
                }
                this.Controls.Add(TLPmain);
            }
            SetState(StateEnum.Disconnected);
        }
        static Random Rand = new Random();
        private void TXBinput_TextChanged(object sender, EventArgs e)
        {
            var text = (sender as MyTextBox).Text;
            if (text.Length == 0 || !text.EndsWith("\r\n")) return;
            var idx = text.IndexOf("\r\n");
            (sender as MyTextBox).Text = text.Substring(idx + 2);
            text = text.Remove(idx);
            if(text.StartsWith("/")||State!=StateEnum.Chatting)
            {
                if (text.StartsWith("/c")) ButtonClicked(BTNconnect, null);
                else if (text.StartsWith("/t")) ButtonClicked(BTNmatch, null);
                else if (text.StartsWith("/q")) ButtonClicked(BTNquit, null);
                else Msg($"無此指令：{text}");
            }
            else
            {
                text = Encode(text);
                var msg = JsonConvert.SerializeObject(new Json.SendMessage
                {
                    message = text,
                    sequence = Rand.Next(int.MaxValue)
                });
                lock(manager.syncRoot)
                {
                    manager.Send(msg);
                    while ((msg = manager.Receive()) == null) Application.DoEvents();
                    Trace.Assert(JsonConvert.DeserializeObject<Json.SendMessage>(msg).message == text);
                    Log($"你傳了：{text}");
                    TXBchat.AppendText($"你傳了：{Decode(text)}\r\n");
                }
            }
        }
        string Encode(string s)
        {
            string ans = "";
            char[] data = s.ToCharArray();
            for (int i = 0; i < data.Length; i++)
            {
                byte[] bytes = Encoding.Unicode.GetBytes(data[i].ToString());
                ans += @"\u" + bytes[1].ToString("X2") + bytes[0].ToString("X2");
            }
            return ans;
        }
        string Decode(string s)
        {
            Trace.Assert(s.Length % 6 == 0);
            if (s.Length == 0) return "";
            var data = s.Substring(1).Split('\\').Select(v=>v.Substring(1));
            string ans = "";
            foreach (string d in data) ans += Encoding.Unicode.GetString(new byte[2]
             {
                (byte) int.Parse(d.Substring(2,2),System.Globalization.NumberStyles.HexNumber),
                (byte) int.Parse(d.Substring(0,2),System.Globalization.NumberStyles.HexNumber)
             });
            return ans;
        }
        void Log(string msg)
        {
            log.Log(msg);
        }
        void Msg(string msg)
        {
            Log(msg);
            TXBmsg.AppendText($"{msg}\r\n");
        }
        void Show(string msg)
        {
            Msg(msg);
            MessageBox.Show(msg);
        }
        void Send(string msg)
        {
            Log("Sending...");
            Log(msg);
            manager.Send(msg);
            Log("Sent");
        }
        string Receive()
        {
            Log("Receiving...");
            string ans = manager.Receive();
            if (ans != null)
            {
                Log(ans);
                Log("Received");
            }
            return ans;
        }
        class Json
        {
            public class TryMatch
            {
                public string cmd="try_match",name;
                public int age;
                public string gender, introduction, filter_function;
            }
            public class Matched
            {
                public string cmd = "matched", name;
                public int age;
                public string gender, introduction, filter_function;
            }
            public class SendMessage
            {
                public string cmd = "send_message", message;
                public int sequence;
            }
            public class ReceiveMessage
            {
                public string cmd = "receive_message", message;
                public int sequence;
            }
            public class Quit
            {
                public string cmd = "quit";
            }
        }
        private async void ButtonClicked(object sender, EventArgs e)
        {
            if(!(sender as MyButton).Enabled)
            {
                Msg($"沒有作用的指令：{(sender as MyButton).Text}");
                return;
            }
            switch((sender as MyButton).Text)
            {
                case "/c (連線至伺服器)":
                    {
                        try
                        {
                            if (State != StateEnum.Disconnected) return;
                            Msg($"正在連線...ip={info.ip.Text},port={info.port.Text}");
                            manager.Connect(info.ip.Text, int.Parse(info.port.Text));
                            Msg($"連線成功！");
                            SetState(StateEnum.Idle);
                        }
                        catch(Exception error) { Show(error.ToString()); }
                    } break;
                case "/c (結束網路連線)":
                    {
                        try
                        {
                            if (State == StateEnum.Disconnected) return;
                            Msg($"正在關閉連線...");
                            manager.Disconnect();
                            Msg($"已成功關閉連線！");
                            SetState(StateEnum.Disconnected);
                        }
                        catch(Exception error) { Show(error.ToString()); }
                    }break;
                case "/t (嘗試匹配)":
                    {
                        try
                        {
                            if (State != StateEnum.Idle) return;
                            var msg = JsonConvert.SerializeObject(new Json.TryMatch
                            {
                                name = info.name.Text,
                                age = int.Parse(info.age.Text),
                                gender = info.gender.Text,
                                introduction = info.introduction.Text,
                                filter_function = info.filter_function.Text
                            });
                            Send(msg);
                            while (Receive() == null)
                            {
                                await Task.Delay(500);
                                Application.DoEvents();
                            }
                            Msg("匹配中...");
                            SetState(StateEnum.Matching);
                            while(State==StateEnum.Matching)
                            {
                                Application.DoEvents();
                                await Task.Delay(500);
                                if((msg=Receive())!=null)
                                {
                                    Msg("成功匹配");
                                    var partner = JsonConvert.DeserializeObject<Json.Matched>(msg);
                                    TXBchat.AppendText($"對方資訊：\r\n");
                                    TXBchat.AppendText($"暱稱：{partner.name}\r\n");
                                    TXBchat.AppendText($"年齡：{partner.age}\r\n");
                                    TXBchat.AppendText($"性別：{partner.gender}\r\n");
                                    TXBchat.AppendText($"自介：{partner.introduction}\r\n");
                                    SetState(StateEnum.Chatting);
                                }
                            }
                            while(State==StateEnum.Chatting)
                            {
                                Application.DoEvents();
                                await Task.Delay(500);
                                if ((msg = Receive()) != null)
                                {
                                    var msgObj = JsonConvert.DeserializeObject<Json.ReceiveMessage>(msg);
                                    if (msgObj.cmd == "other_side_quit")
                                    {
                                        Msg($"對方已離開");
                                        SetState(StateEnum.Idle);
                                    }
                                    else
                                    {
                                        Log($"對方回覆：{msgObj.message}");
                                        TXBchat.AppendText($"對方回覆：{Decode(msgObj.message)}\r\n");
                                    }
                                }
                            }
                        }
                        catch(Exception error) { Show(error.ToString()); }
                    }break;
                case "/q (放棄匹配)":
                    {
                        try
                        {
                            if (State != StateEnum.Matching) return;
                            var msg = JsonConvert.SerializeObject(new Json.Quit());
                            lock (manager.syncRoot)
                            {
                                manager.Send(msg);
                                while ((msg = manager.Receive()) == null) Application.DoEvents();
                                Trace.Assert(JsonConvert.DeserializeObject<Json.Quit>(msg).cmd == "quit");
                                SetState(StateEnum.Idle);
                            }
                        }
                        catch (Exception error) { Show(error.ToString()); }
                    }break;
                case "/q (結束聊天)":
                    {
                        try
                        {
                            if (State != StateEnum.Chatting) return;
                            var msg = JsonConvert.SerializeObject(new Json.Quit());
                            lock (manager.syncRoot)
                            {
                                manager.Send(msg);
                                while ((msg = manager.Receive()) == null) Application.DoEvents();
                                Trace.Assert(JsonConvert.DeserializeObject<Json.Quit>(msg).cmd == "quit");
                                SetState(StateEnum.Idle);
                            }
                        }
                        catch (Exception error) { Show(error.ToString()); }
                    }
                    break;
            }
        }
        public void Start() { TXBinput.Text = "/c\r\n"; }
    }
    public class AboutPage:MyTabPage
    {
        public AboutPage():base("這爛程式是誰做的？")
        {
            this.Controls.Add(new MyTextBox(true, "↓跟這些有關哦～↓\r\nfsps60312\r\nhttps://codingsimplifylife.blogspot.tw/\r\nhttps://www.facebook.com/CodingSimplifyLife/\r\n\r\n歡迎來玩Code風景區的Chatbot，玩法是私訊粉專！>///<\r\n\r\n此程式源自於我們系統程式設計的某一次作業，詳情請看：\r\nhttps://systemprogrammingatntu.github.io/MP4\r\n\r\n此程式已開源：\r\nhttps://github.com/fsps60312/SP-MP4-Windows-Client"));
        }
    }
    public class InfoPage:MyTabPage
    {
        public class TextPage:MyTabPage
        {
            public new string Text { get { return TXB.Text; } }
            public MyTextBox TXB;
            public TextPage(string text,string content) : base(text)
            {
                {
                    TXB = new MyTextBox(true,content);
                    this.Controls.Add(TXB);
                }
            }
        }
        MyTabControl TCmain;
        public TextPage name, age, gender, introduction, filter_function,ip,port;
        public InfoPage():base("個人資訊")
        {
            {
                TCmain = new MyTabControl();
                TCmain.TabPages.Add(name = new TextPage("暱稱 (name)","loser"));
                TCmain.TabPages.Add(age = new TextPage("年齡 (age)","23"));
                TCmain.TabPages.Add(gender = new TextPage("性別 (gender)","male"));
                TCmain.TabPages.Add(introduction = new TextPage("自介 (introduction)","I get hurt a lot in that chatroom war..."));
                TCmain.TabPages.Add(filter_function = new TextPage("篩選函式 (filter_function)","int filter_function(struct User user)\r\n" +
                    "{\r\n" +
                    "    if(user.age<18 || user.age>25) return 0;//這樣可以篩掉不是大學年紀的人\r\n" +
                    "    return 1;\r\n" +
                    "}\r\n" +
                    "/*(修改上面這個函式來篩選欲匹配的對象，下面是User的定義。程式語言：C(不是C++))\r\n" +
                    "struct User {\r\n" +
                    "    char name[33],\r\n" +
                    "    unsigned int age,\r\n" +
                    "    char gender[7],\r\n" +
                    "    char introduction[1025]\r\n" +
                    "};*/"));
                filter_function.TXB.Font = new Font("Consolas", 15);
                TCmain.TabPages.Add(ip = new TextPage("IP", "140.112.30.32"));
                TCmain.TabPages.Add(port = new TextPage("Port", "10000"));
                this.Controls.Add(TCmain);
            }
        }
    }
    public class LogPage:MyTabPage
    {
        public void Log(string msg) { TXBlog.AppendText($"{DateTime.Now}\t{msg}\r\n"); }
        MyTextBox TXBlog;
        public LogPage():base("Log")
        {
            {
                TXBlog = new MyTextBox(true);
                this.Controls.Add(TXBlog);
            }
        }
    }
    public partial class Form1 : Form
    {
        MyTabControl TCmain;
        MessagePage TPmsg;
        InfoPage TPinfo;
        LogPage TPlog;
        void Log(string msg) { TPlog.Log(msg); }
        public Form1()
        {
            this.Text = "NTU WooTalk";
            this.Icon = Properties.Resources.clock_reveal_00095_600;
            this.Size = new Size(1000, 700);
            {
                TCmain = new MyTabControl();
                {
                    TPlog = new LogPage();
                    TPinfo = new InfoPage();
                    TPmsg = new MessagePage(TPinfo,TPlog);
                    TCmain.TabPages.Add(TPmsg);
                }
                {
                    TCmain.TabPages.Add(TPinfo);
                }
                {
                    TCmain.TabPages.Add(TPlog);
                }
                {
                    TCmain.TabPages.Add(new AboutPage());
                }
                this.Controls.Add(TCmain);
            }
            this.FormClosing += Form1_FormClosing;
            Log("初始化完成！");
            this.Shown += Form1_Shown;
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            TPmsg.Start();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Process.GetCurrentProcess().Kill();
        }
    }
}
