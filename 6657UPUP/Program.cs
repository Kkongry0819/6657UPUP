using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

namespace MemeCopierApp
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class AppConfig
    {
        public int ProviderIndex { get; set; } = 1;
        public string ApiBaseUrl { get; set; } = "https://api.deepseek.com/chat/completions";
        public string ApiModel { get; set; } = "deepseek-chat";
        public string ApiKey { get; set; } = "";
    }

    public class MainForm : Form
    {
        private const string AiPromptTemplate = @"你现在是一个精通中文互联网抽象文化、常年混迹于B站、斗鱼鱼吧、HLTV评论区、各大游戏直播间（尤其是CS2）和二次元社群的“资深串子”与“烂梗制造机”。你极度了解各种小众黑话、主播圣经、电竞选手的特征以及二次元名场面。

你的任务是生成极度抽象、有节目效果、甚至带点“发癫”和“地狱笑话”性质的互联网烂梗、弹幕或段子。

【生成规则与技巧】：
1. 极度缝合：将毫不相干的圈子强行结合（例如：把玩机器和CS2战队结合，把MyGO的角色和现实主播结合）。
2. 高浓度黑话与外号：不能使用全名或官方称呼。必须使用黑话（如：载物、老尼、软脚虾、qu总、玩大哥、大b哥、总监、马西西、小驴等）。
3. 经典句式与标点：
   - 使用书名号《》表示连续的、逐渐离谱的弹幕或对话。例如：@巴西老头：《狂哥是一个不杀啊》《尤里玩不玩啊》《水蛭又在吸队友》《哎教父也该接一个的》/《虽然我枪法差，但是我人品GOAT》《尼蔻搞小团体》《老汤饰品借了不还》《表哥抛队弃友》《段位B＋》
   - 使用方括号【】后加一句自编的七言律诗伪造极其吸睛、夸张的新闻标题或贴吧标题。例如：【HLTV】冲天香阵透长安， 满城尽带黄金甲！里约iem场馆宣布在接下来黑豹的现场比赛中取消观众安检步骤
   - 使用“@+人名：+灵魂发问”的句式伪造群聊或对话记录。
   - 大量使用双引号“”制造强烈的戏剧冲突感。
4. 抽象Emoji：在句末或句中穿插能产生奇妙联想的Emoji组合（如：🦅、🎂、🍅）。
5. 语气要求：谜语人、阴阳怪气、发癫、破防、或者强装理智的胡言乱语。不要任何解释说明，直接输出梗本身。

【参考范例】：
- {0}
- 【HLTV】子不教，父之过！dev1ce宣布加入100T来提醒儿子承诺的100个俯卧撑！
- @🗿：无人在意的角落，居然也存在着几位研发员，就像是废墟里的花朵
- 北京四季酒店大厅外放玩机器还带四五个西装保镖的老头声音小点好吗？
- 就tm喜欢银行业🥰银行坏账我喜欢🥰银行挤兑我还喜欢😍 利率调低我还喜欢🤤 老子亲烂银行业的脸😘

【开始执行】：
生成1条25字以内的符合上述风格的烂梗段子";

        private readonly string configFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        private AppConfig appConfig = new AppConfig();

        private List<string> allMemes = new List<string>();
        private List<string> displayedMemes = new List<string>();
        private HashSet<string> selectedTags = new HashSet<string>();
        private Random random = new Random();
        private CancellationTokenSource fetchCts;
        private bool isFetching = false;

        private readonly Color colorBackground = ColorTranslator.FromHtml("#F0F2F5");
        private readonly Color colorCard = Color.White;
        private readonly Color colorPrimary = ColorTranslator.FromHtml("#409EFF");
        private readonly Color colorDanger = ColorTranslator.FromHtml("#F56C6C");
        private readonly Color colorSuccess = ColorTranslator.FromHtml("#67C23A");
        private readonly Color colorAI = ColorTranslator.FromHtml("#8A2BE2");
        private readonly Color colorTextMain = ColorTranslator.FromHtml("#303133");

        private FlowLayoutPanel flpTags;
        private WebView2 webViewMemes;
        private Panel panelList;
        private Button btnFetch, btnGenerateAi, btnRandomCopy, btnAbout;
        private Label lblStatus;
        private TextBox txtSearch, txtApiUrl, txtApiKey, txtApiModel;
        private ComboBox cmbProvider;

        private Panel panelAbout;

        private readonly Dictionary<string, string> predefinedTags = new Dictionary<string, string>
        {
            {"喷玩机器", "00"}, {"喷选手", "01"}, {"加一", "02"}, {"QUQU", "03"},
            {"木柜子", "05"}, {"群魔乱舞", "06"}, {"NiKo", "07"}, {"ropz", "08"},
            {"直播间互喷", "09"}, {"Donk", "10"}, {"伟伟", "11"}, {"Zywoo", "12"},
            {"m0NESY", "13"}, {"丰川祥子", "14"}, {"device", "15"}, {"Twistzz", "16"},
            {"DOTA", "17"}, {"千早爱音", "18"}, {"三角初华", "19"}, {"Falcons", "20"},
            {"S1mple", "21"}, {"赛事梗", "22"}, {"京介", "23"}, {"HLTV", "24"},
            {"Team Spirit", "25"}, {"chopper", "26"}, {"🗿🗿🗿", "27"}
        };

        public MainForm()
        {
            LoadConfig();
            InitializeComponent();
            PopulateTags();
            InitializeWebView();
            this.FormClosing += (s, e) => SaveConfig();
        }

        private void InitializeComponent()
        {
            this.Size = new Size(750, 920);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = colorBackground;
            this.Font = new Font("微软雅黑", 10F);
            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch { }

            Panel panelHeader = new Panel { Dock = DockStyle.Top, Height = 50 };

            btnAbout = new Button
            {
                Text = "ℹ️",
                Location = new Point(10, 10),
                Size = new Size(110, 30),
                FlatStyle = FlatStyle.Flat,
                ForeColor = colorTextMain,
                BackColor = colorBackground,
                Cursor = Cursors.Hand,
                Font = new Font("微软雅黑", 9F, FontStyle.Regular)
            };
            btnAbout.FlatAppearance.BorderSize = 0;
            btnAbout.FlatAppearance.MouseOverBackColor = ControlPaint.Light(colorBackground);
            btnAbout.Click += BtnAbout_Click;

            Label lblTitle = new Label
            {
                Text = "⚡6657烂梗复制器",
                Font = new Font("微软雅黑", 15F, FontStyle.Bold),
                ForeColor = colorTextMain,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter
            };

            panelHeader.Controls.Add(btnAbout);
            panelHeader.Controls.Add(lblTitle);
            btnAbout.BringToFront();
            this.Controls.Add(panelHeader);

            Panel panelFetch = CreateCardPanel(new Point(20, 60), new Size(this.ClientSize.Width - 40, 180));
            panelFetch.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            flpTags = new FlowLayoutPanel { Location = new Point(15, 35), Size = new Size(panelFetch.Width - 30, 95), AutoScroll = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            btnFetch = CreateFlatButton("⬇️ 开启 Chromium 瀑布流拉取", colorPrimary, new Point(15, 135), new Size(panelFetch.Width - 30, 35));
            btnFetch.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            btnFetch.Click += BtnFetch_Click;
            panelFetch.Controls.AddRange(new Control[] { new Label { Text = "云端 Tags 筛选:", Location = new Point(15, 10), Size = new Size(300, 20), Font = new Font("微软雅黑", 10F, FontStyle.Bold) }, flpTags, btnFetch });
            this.Controls.Add(panelFetch);

            Panel panelAi = CreateCardPanel(new Point(20, 250), new Size(this.ClientSize.Width - 40, 180));
            panelAi.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            txtSearch = new TextBox { Location = new Point(120, 15), Size = new Size(panelAi.Width - 130, 25), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            txtSearch.TextChanged += TxtSearch_TextChanged;

            cmbProvider = new ComboBox { Location = new Point(120, 55), Size = new Size(130, 25), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbProvider.Items.AddRange(new string[] { "OpenAI", "DeepSeek", "Gemini" });
            cmbProvider.SelectedIndex = appConfig.ProviderIndex;
            cmbProvider.SelectedIndexChanged += CmbProvider_SelectedIndexChanged;

            txtApiModel = new TextBox { Location = new Point(310, 55), Size = new Size(panelAi.Width - 320, 25), Text = appConfig.ApiModel, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            txtApiUrl = new TextBox { Location = new Point(120, 95), Size = new Size(panelAi.Width - 130, 25), Text = appConfig.ApiBaseUrl, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            txtApiKey = new TextBox { Location = new Point(120, 135), Size = new Size(panelAi.Width - 275, 25), PasswordChar = '•', Text = appConfig.ApiKey, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            btnGenerateAi = CreateFlatButton("✨ 创作新梗", colorAI, new Point(panelAi.Width - 130, 132), new Size(120, 32));
            btnGenerateAi.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnGenerateAi.Click += BtnGenerateAi_Click;

            panelAi.Controls.AddRange(new Control[] {
                new Label { Text = "🔍 内容筛选:", Location = new Point(15, 18), Size = new Size(105, 25), Font = new Font("微软雅黑", 10F, FontStyle.Bold) }, txtSearch,
                new Label { Text = "⚙️ 引擎选择:", Location = new Point(15, 58), Size = new Size(105, 25), Font = new Font("微软雅黑", 10F, FontStyle.Bold) }, cmbProvider,
                new Label { Text = "模型:", Location = new Point(255, 58), Size = new Size(50, 25), Font = new Font("微软雅黑", 10F, FontStyle.Bold) }, txtApiModel,
                new Label { Text = "🌐 接口地址:", Location = new Point(15, 98), Size = new Size(105, 25), Font = new Font("微软雅黑", 10F, FontStyle.Bold) }, txtApiUrl,
                new Label { Text = "🤖 API Key:", Location = new Point(15, 138), Size = new Size(105, 25), Font = new Font("微软雅黑", 10F, FontStyle.Bold) }, txtApiKey, btnGenerateAi
            });
            this.Controls.Add(panelAi);

            Panel panelBottom = CreateCardPanel(new Point(20, this.ClientSize.Height - 110), new Size(this.ClientSize.Width - 40, 90));
            panelBottom.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            btnRandomCopy = CreateFlatButton("🎲 随机抽取 (瀑布流面板支持右键直拷)", colorSuccess, new Point(15, 15), new Size(panelBottom.Width - 30, 45));
            btnRandomCopy.Font = new Font("微软雅黑", 12F, FontStyle.Bold);
            btnRandomCopy.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            btnRandomCopy.Click += BtnRandomCopy_Click;
            lblStatus = new Label { Text = "状态: 浏览器内核初始化中...", Location = new Point(15, 65), Size = new Size(panelBottom.Width - 30, 20), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            panelBottom.Controls.AddRange(new Control[] { btnRandomCopy, lblStatus });
            this.Controls.Add(panelBottom);

            panelList = CreateCardPanel(new Point(20, 440), new Size(this.ClientSize.Width - 40, this.ClientSize.Height - 560));
            panelList.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            this.Controls.Add(panelList);

            panelAbout = CreateCardPanel(new Point(20, 45), new Size(380, 300));
            panelAbout.Visible = false;

            Label lblAboutText = new Label
            {
                Text = "【⚡ 6657烂梗复制器】\n\n" +
                       "🛠️ 开发与设计：Kkongry0819(空空）\n\n" +
                       "🌟 特别鸣谢：\n" +
                       "1. 感谢 sb6657.cn 提供的接口与数据。\n" +
                       "2. 感谢 WebView2 社区提供的跨平台引擎。\n" +
                       "3. 感谢 玩机器丶Machine对CS比赛生态的付出。\n" +
                       "4. 感谢 DeepSeek / OpenAI / Gemini 提供AI支持。\n\n" +
                       "本软件仅供学习与娱乐交流使用，玩梗适度！",
                Location = new Point(20, 20),
                Size = new Size(340, 210),
                Font = new Font("微软雅黑", 9.5F),
                ForeColor = colorTextMain
            };

            Button btnCloseAbout = CreateFlatButton("我知道了", colorPrimary, new Point(20, 250), new Size(340, 32));
            btnCloseAbout.Click += (s, e) => panelAbout.Visible = false;

            panelAbout.Controls.AddRange(new Control[] { lblAboutText, btnCloseAbout });
            this.Controls.Add(panelAbout);
            panelAbout.BringToFront();
        }

        private void BtnAbout_Click(object sender, EventArgs e)
        {
            panelAbout.Visible = !panelAbout.Visible;
            if (panelAbout.Visible)
            {
                panelAbout.BringToFront();
            }
        }

        private void CmbProvider_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = cmbProvider.SelectedIndex;
            if (index == 0)
            {
                txtApiUrl.Text = "https://api.openai.com/v1/chat/completions";
                txtApiModel.Text = "gpt-3.5-turbo";
            }
            else if (index == 1)
            {
                txtApiUrl.Text = "https://api.deepseek.com/chat/completions";
                txtApiModel.Text = "deepseek-chat";
            }
            else if (index == 2)
            {
                txtApiUrl.Text = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";
                txtApiModel.Text = "gemini-1.5-flash";
            }
        }

        private async void InitializeWebView()
        {
            btnFetch.Enabled = false; btnGenerateAi.Enabled = false;
            webViewMemes = new WebView2 { Dock = DockStyle.Fill };
            panelList.Controls.Add(webViewMemes);

            try
            {
                string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "6657langeng");
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await webViewMemes.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex)
            {
                MessageBox.Show("浏览器内核初始化失败: " + ex.Message);
            }

            webViewMemes.WebMessageReceived += WebView_WebMessageReceived;

            string html = @"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <link href='https://fonts.googleapis.com/css2?family=Noto+Color+Emoji&display=swap' rel='stylesheet'>
                <style>
                    body { font-family: 'Noto Color Emoji', 'Microsoft YaHei', sans-serif; background: #ffffff; color: #303133; margin: 0; padding: 0; font-size: 14px;}
                    ::-webkit-scrollbar { width: 6px; }
                    ::-webkit-scrollbar-track { background: transparent; }
                    ::-webkit-scrollbar-thumb { background: #dcdfe6; border-radius: 3px; }
                    .meme { padding: 10px 15px; border-bottom: 1px solid #ebeef5; cursor: pointer; word-break: break-all; transition: background 0.1s;}
                    .meme:hover { background-color: #f5f7fa; }
                </style>
            </head>
            <body>
                <div id='list'></div>
                <script>
                    const list = document.getElementById('list');
                    function clearList() { list.innerHTML = ''; }
                    function appendMemes(memes) {
                        memes.forEach((m) => {
                            const div = document.createElement('div');
                            div.className = 'meme'; div.innerText = m;
                            div.oncontextmenu = (e) => { e.preventDefault(); window.chrome.webview.postMessage(m); };
                            list.appendChild(div);
                        });
                    }
                </script>
            </body>
            </html>";
            webViewMemes.NavigateToString(html);
            btnFetch.Enabled = true; btnGenerateAi.Enabled = true;
            lblStatus.Text = "状态: Chromium 内核加载完毕，随时待命！";
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            string selectedText = e.TryGetWebMessageAsString();
            if (!string.IsNullOrEmpty(selectedText))
            {
                string textToCopy = selectedText.Replace("[AI创造] ", "");
                try { Clipboard.SetText(textToCopy); lblStatus.Text = $"[右键秒拷] 已复制 ✨ -> {textToCopy}"; lblStatus.ForeColor = colorSuccess; }
                catch { lblStatus.Text = "复制失败"; lblStatus.ForeColor = Color.Red; }
            }
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e) { UpdateWebViewDisplay(); }

        private void UpdateWebViewDisplay()
        {
            string keyword = txtSearch.Text.Trim(); displayedMemes.Clear();
            foreach (var meme in allMemes)
                if (string.IsNullOrEmpty(keyword) || meme.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    displayedMemes.Add(meme);

            if (webViewMemes != null && webViewMemes.CoreWebView2 != null)
            {
                var options = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                string json = JsonSerializer.Serialize(displayedMemes, options);
                webViewMemes.CoreWebView2.ExecuteScriptAsync($"clearList(); appendMemes({json});");
            }
        }

        private async void BtnFetch_Click(object sender, EventArgs e)
        {
            if (isFetching) { fetchCts?.Cancel(); return; }
            isFetching = true; fetchCts = new CancellationTokenSource();
            btnFetch.Text = "⏸ 停止加载 (Chromium 疯狂渲染中...)"; btnFetch.BackColor = colorDanger;
            allMemes.Clear(); displayedMemes.Clear();
            if (webViewMemes != null && webViewMemes.CoreWebView2 != null) webViewMemes.CoreWebView2.ExecuteScriptAsync("clearList();");

            int currentPage = 1; bool lastPage = false;
            string tagsParam = selectedTags.Count > 0 ? $"&tags={string.Join(",", selectedTags)}" : "";
            string currentSearch = txtSearch.Text.Trim();

            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator };
            using (HttpClient client = new HttpClient(handler))
            {
                try
                {
                    while (!lastPage && !fetchCts.Token.IsCancellationRequested)
                    {
                        lblStatus.Text = $"正在拉取第 {currentPage} 页... 当前总计 {allMemes.Count} 条"; lblStatus.ForeColor = colorPrimary;
                        string jsonResponse = await client.GetStringAsync($"https://hguofichp.cn:10086/machine/Page?pageNum={currentPage}&pageSize=50{tagsParam}");

                        using (JsonDocument doc = JsonDocument.Parse(jsonResponse))
                        {
                            JsonElement root = doc.RootElement;
                            if (root.TryGetProperty("code", out JsonElement codeElement) && codeElement.GetInt32() == 200)
                            {
                                if (root.TryGetProperty("data", out JsonElement dataObj))
                                {
                                    if (dataObj.TryGetProperty("lastPage", out JsonElement lpElement)) lastPage = lpElement.GetBoolean();
                                    if (dataObj.TryGetProperty("list", out JsonElement listObj) && listObj.ValueKind == JsonValueKind.Array)
                                    {
                                        List<string> newItems = new List<string>();
                                        foreach (JsonElement item in listObj.EnumerateArray())
                                        {
                                            if (item.TryGetProperty("barrage", out JsonElement barrageElement))
                                            {
                                                string text = barrageElement.GetString()?.Replace("\r", "")?.Replace("\n", " ");
                                                if (!string.IsNullOrWhiteSpace(text))
                                                {
                                                    allMemes.Add(text);
                                                    if (string.IsNullOrEmpty(currentSearch) || text.IndexOf(currentSearch, StringComparison.OrdinalIgnoreCase) >= 0)
                                                    {
                                                        displayedMemes.Add(text); newItems.Add(text);
                                                    }
                                                }
                                            }
                                        }
                                        if (newItems.Count > 0 && webViewMemes != null && webViewMemes.CoreWebView2 != null)
                                        {
                                            var options = new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                                            webViewMemes.CoreWebView2.ExecuteScriptAsync($"appendMemes({JsonSerializer.Serialize(newItems, options)});");
                                        }
                                    }
                                }
                            }
                        }
                        currentPage++; await Task.Delay(200);
                    }
                    lblStatus.Text = fetchCts.Token.IsCancellationRequested ? $"已停止。共 {allMemes.Count} 条数据。" : $"全部加载完毕！共 {allMemes.Count} 条数据。";
                    lblStatus.ForeColor = colorSuccess;
                }
                catch (Exception ex) { lblStatus.Text = "拉取中断: " + ex.Message; lblStatus.ForeColor = Color.Red; }
                finally
                {
                    isFetching = false; btnFetch.Text = "⬇️ 开启 Chromium 瀑布流拉取"; btnFetch.BackColor = colorPrimary;
                    fetchCts?.Dispose();
                }
            }
        }

        private async void BtnGenerateAi_Click(object sender, EventArgs e)
        {
            SaveConfig();
            string apiKey = txtApiKey.Text.Trim();
            string apiUrl = txtApiUrl.Text.Trim();
            string apiModel = txtApiModel.Text.Trim();
            int providerIndex = cmbProvider.SelectedIndex;

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiUrl)) { MessageBox.Show("请确保 API 地址和 Key 填写完整！", "提示"); return; }
            if (allMemes.Count < 3) { MessageBox.Show("库存太少，请先拉取一些数据以供 AI 学习风格！", "提示"); return; }

            btnGenerateAi.Enabled = false; btnGenerateAi.Text = "神经连接中...";
            lblStatus.Text = $"正在通过 {cmbProvider.Text} 引擎融合思维..."; lblStatus.ForeColor = colorAI;

            try
            {
                var sourceList = displayedMemes.Count >= 3 ? displayedMemes : allMemes;
                string contextText = string.Join("，\n", sourceList.OrderBy(x => random.Next()).Take(15).ToList());

                string prompt = string.Format(AiPromptTemplate, contextText);

                using (HttpClient client = new HttpClient())
                {
                    HttpResponseMessage response = null;
                    string newMeme = null;

                    if (providerIndex == 0 || providerIndex == 1)
                    {
                        var requestBody = new
                        {
                            model = apiModel,
                            messages = new[] {
                                new { role = "system", content = "你是一个深谙互联网文化、擅长玩梗发疯的网络乐子人。" },
                                new { role = "user", content = prompt }
                            },
                            temperature = 0.9,
                            max_tokens = 50
                        };

                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                        response = await client.PostAsync(apiUrl, content);

                        if (response.IsSuccessStatusCode)
                        {
                            using (JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
                            {
                                newMeme = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString()?.Trim();
                            }
                        }
                    }
                    else if (providerIndex == 2)
                    {
                        var requestBody = new
                        {
                            systemInstruction = new { parts = new[] { new { text = "你是一个深谙互联网文化、擅长玩梗发疯的网络乐子人。" } } },
                            contents = new[] { new { parts = new[] { new { text = prompt } } } },
                            generationConfig = new { temperature = 0.9, maxOutputTokens = 50 }
                        };

                        string fullUrl = apiUrl.Contains("?") ? $"{apiUrl}&key={apiKey}" : $"{apiUrl}?key={apiKey}";
                        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
                        response = await client.PostAsync(fullUrl, content);

                        if (response.IsSuccessStatusCode)
                        {
                            using (JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
                            {
                                newMeme = doc.RootElement.GetProperty("candidates")[0]
                                    .GetProperty("content").GetProperty("parts")[0]
                                    .GetProperty("text").GetString()?.Trim();
                            }
                        }
                    }

                    if (response != null && response.IsSuccessStatusCode && !string.IsNullOrEmpty(newMeme))
                    {
                        string aiMeme = $"[AI创造] {newMeme}";
                        allMemes.Insert(0, aiMeme);
                        UpdateWebViewDisplay();
                        lblStatus.Text = $"✨ {cmbProvider.Text} 创造成功！已登顶。"; lblStatus.ForeColor = colorAI;
                    }
                    else
                    {
                        lblStatus.Text = $"{cmbProvider.Text} 失败: " + (response != null ? response.StatusCode.ToString() : "未收到响应");
                        lblStatus.ForeColor = Color.Red;
                    }
                }
            }
            catch (Exception ex) { lblStatus.Text = "AI 异常: " + ex.Message; lblStatus.ForeColor = Color.Red; }
            finally { btnGenerateAi.Text = "✨ 创作新梗"; btnGenerateAi.Enabled = true; }
        }

        private void BtnRandomCopy_Click(object sender, EventArgs e)
        {
            if (displayedMemes.Count == 0) { MessageBox.Show("列表为空！", "提示"); return; }
            int index = random.Next(displayedMemes.Count);
            string textToCopy = displayedMemes[index].Replace("[AI创造] ", "");
            try { Clipboard.SetText(textToCopy); lblStatus.Text = $"[随机抽取] 已复制 ✨ -> {textToCopy}"; lblStatus.ForeColor = colorSuccess; }
            catch { lblStatus.Text = "复制失败"; lblStatus.ForeColor = Color.Red; }
        }

        private void LoadConfig()
        {
            try
            {
                string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "6657langeng");
                string finalConfigPath = Path.Combine(appDataFolder, "config.json");

                if (File.Exists(finalConfigPath))
                {
                    var conf = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(finalConfigPath));
                    if (conf != null) appConfig = conf;
                }
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "6657langeng");
                if (!Directory.Exists(appDataFolder)) Directory.CreateDirectory(appDataFolder);

                string finalConfigPath = Path.Combine(appDataFolder, "config.json");

                appConfig.ProviderIndex = cmbProvider.SelectedIndex;
                appConfig.ApiBaseUrl = txtApiUrl.Text.Trim();
                appConfig.ApiModel = txtApiModel.Text.Trim();
                appConfig.ApiKey = txtApiKey.Text.Trim();

                File.WriteAllText(finalConfigPath, JsonSerializer.Serialize(appConfig));
            }
            catch { }
        }

        private void PopulateTags()
        {
            foreach (var tag in predefinedTags)
            {
                CheckBox chk = new CheckBox { Text = tag.Key, Tag = tag.Value, Appearance = Appearance.Button, FlatStyle = FlatStyle.Flat, AutoSize = true, Cursor = Cursors.Hand, BackColor = colorBackground, ForeColor = colorTextMain, Margin = new Padding(3) };
                chk.FlatAppearance.BorderSize = 0; chk.FlatAppearance.CheckedBackColor = colorPrimary;
                chk.CheckedChanged += (s, e) => { if (chk.Checked) { chk.ForeColor = Color.White; selectedTags.Add((string)chk.Tag); } else { chk.ForeColor = colorTextMain; selectedTags.Remove((string)chk.Tag); } };
                flpTags.Controls.Add(chk);
            }
        }
        private Panel CreateCardPanel(Point location, Size size) { var p = new Panel { Location = location, Size = size, BackColor = colorCard }; p.Paint += (s, e) => ControlPaint.DrawBorder(e.Graphics, p.ClientRectangle, ColorTranslator.FromHtml("#DCDFE6"), ButtonBorderStyle.Solid); return p; }
        private Button CreateFlatButton(string text, Color backColor, Point location, Size size) { return new Button { Text = text, Location = location, Size = size, BackColor = backColor, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, FlatAppearance = { BorderSize = 0, MouseOverBackColor = ControlPaint.Light(backColor), MouseDownBackColor = ControlPaint.Dark(backColor) }, Cursor = Cursors.Hand, Font = new Font("微软雅黑", 10F, FontStyle.Bold) }; }
    }
}