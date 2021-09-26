using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using MetroFramework.Forms;
using System.Net;
using System.Net.Http;
using System.IO;
using test.Properties;

namespace test
{
    public partial class Form1 : MetroForm
    {
        private static clients cli = new clients();

        private static readonly string PATH = @Directory.GetCurrentDirectory();

        private static int concurrency = 1;
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Activate();
        }

        private async Task<bool> MakeJOB(int pos)
        {
            return await cli.NewRequest<bool>((HttpClient client) =>
            {
                try
                {
                    if (cli.handler != null)
                    {
                        var handler = cli.GethandlerIndexed(pos);
                        client = new HttpClient(handler);
                    }
                    else
                        client = new HttpClient();

                    cli.AssignDefaultHeaders(client);

                    using (HttpResponseMessage response = client.GetAsync("https://api.my-ip.io/ip.txt").Result)
                    using (HttpContent content = response.Content)
                        this.lblUrl.TextInvoke(content.ReadAsStringAsync().Result + " / " + Task.CurrentId);
                    return true;
                }
                catch { /* exception .. */ return false; }
            });
        }

        private async void btnStart_CheckedChanged(object sender, EventArgs e)
        {
            if (!this.btnStart.Checked)
            {
                cli.ForceCancelAll();

                return;
            }

            int concurrency;
            if (!int.TryParse(this.txtboxConcur.Text, out concurrency))
                concurrency = 1;

            cli.SetConcurrentDownloads(concurrency);

            var t = new Task[concurrency];
            int pos = 0;
            for (int i = 0; i < t.Length; i++, pos++)
                t[i] = MakeJOB(pos++);
            await Task.WhenAll(t);
        }

        private void btnSetConcur_Click(object sender, EventArgs e)
        {
            int concur = 1;
            if (int.TryParse(this.txtboxConcur.Text, out concur))
                concurrency = concur;
            ServicePointManager.DefaultConnectionLimit = concurrency;
            cli.SetConcurrentDownloads(concurrency);
        }
    }
}
