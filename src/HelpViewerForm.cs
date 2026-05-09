using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal sealed class HelpViewerForm : Form
    {
        private const string HelpResourceName = "PictureOrganizer.PhotoOrganizerHelp.htm";

        public HelpViewerForm()
        {
            Text = "Photo Organizer Help";
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1000, 760);

            WebBrowser browser = new WebBrowser();
            browser.Dock = DockStyle.Fill;
            browser.DocumentText = LoadHelpHtml();
            Controls.Add(browser);
        }

        private static string LoadHelpHtml()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(HelpResourceName))
            {
                if (stream == null)
                {
                    return "<html><body><h1>Help not available</h1><p>The embedded help file could not be loaded.</p></body></html>";
                }

                using (StreamReader reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
