using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gw2Patcher.UI
{
    public partial class formOutputFormat : Form
    {
        public formOutputFormat(Control alignTo)
        {
            InitializeComponent();

            textUpdateOutputFormat.Text = Settings.UpdatesOutputFormat;
            textUpdateOutputFormat.Select(0, 0);

            if (alignTo != null)
            {
                var p1 = alignTo.PointToScreen(Point.Empty);
                var p2 = textUpdateOutputFormat.PointToScreen(Point.Empty);
                p2.Offset(-this.Location.X, -this.Location.Y);

                this.Location = new Point(p1.X - p2.X + alignTo.Width / 2 - textUpdateOutputFormat.Width / 2, p1.Y - p2.Y + alignTo.Height / 2 - textUpdateOutputFormat.Height / 2);
            }
        }

        public string OutputFormat
        {
            get;
            private set;
        }

        private void buttonGenerateUpdateOutput_Click(object sender, EventArgs e)
        {
            this.OutputFormat = textUpdateOutputFormat.Text;
            this.DialogResult = System.Windows.Forms.DialogResult.OK;
        }

        private void labelVariable_Click(object sender, EventArgs e)
        {
            textUpdateOutputFormat.SelectedText = ((Label)sender).Text;
        }

        private void formOutputFormat_Load(object sender, EventArgs e)
        {

        }
    }
}
