using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace Gw2Patcher.UI
{
    public partial class formWarnings : Form
    {
        public enum Warnings
        {
            NotFound,
            Repeated,
            Uncompressed,
            DownloadError
        }

        private class WarningValue
        {
            private string value;
            private Warnings warning;
            private int count;

            public Warnings Warning
            {
                get
                {
                    return warning;
                }
                set
                {
                    if (warning != value)
                    {
                        count = 0;
                        warning = value;
                        this.value = null;
                    }
                }
            }

            public int Count
            {
                get
                {
                    return count;
                }
                set
                {
                    if (count != value)
                    {
                        count = value;
                        this.value = null;
                    }
                }
            }

            public object Data
            {
                get;
                set;
            }

            public override string ToString()
            {
                var v = this.value;
                if (v != null)
                    return v;

                string ctr;
                if (count > 1)
                    ctr = " (" + count + ")";
                else
                    ctr = "";

                switch (warning)
                {
                    case Warnings.NotFound:
                        value = "Missing" + ctr;
                        break;
                    case Warnings.Repeated:
                        value = "Corrupted" + ctr;
                        break;
                    case Warnings.DownloadError:
                        value = "Download Failed" + ctr;
                        break;
                    case Warnings.Uncompressed:
                        value = "Skipped" + ctr;
                        break;
                    default:
                        return "";
                }

                return value;
            }
        }

        private Dictionary<string, DataGridViewRow> requests;

        public formWarnings()
        {
            InitializeComponent();
            requests = new Dictionary<string, DataGridViewRow>();
        }

        public void SetRequestWarning(string request, Warnings warning, object data)
        {
            DataGridViewRow row;
            DataGridViewCell cell;

            if (!requests.TryGetValue(request, out row))
            {
                requests[request] = row = (DataGridViewRow)gridRequests.RowTemplate.Clone();
                row.CreateCells(gridRequests);
                cell = row.Cells[columnUrl.Index];
                cell.Value = request;

                row.Cells[columnDetails.Index].Value = "Details";


                gridRequests.Rows.Add(row);
            }

            cell = row.Cells[columnStatus.Index];

            WarningValue value;
            if (cell.Value != null)
            {
                value = (WarningValue)cell.Value;
                if (value.Warning != warning)
                    gridRequests.InvalidateCell(cell);
            }
            else
                cell.Value = value = new WarningValue();
            value.Data = data;

            value.Warning = warning;
            value.Count++;
        }

        private void gridRequests_SelectionChanged(object sender, EventArgs e)
        {
            gridRequests.ClearSelection();
        }

        private void formWarnings_Load(object sender, EventArgs e)
        {

        }

        private void gridRequests_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == columnDetails.Index && e.RowIndex >= 0)
            {
                var row = gridRequests.Rows[e.RowIndex];
                var warning = (WarningValue)row.Cells[columnStatus.Index].Value;
                var location = (string)row.Cells[columnUrl.Index].Value;
                var data = warning.Data;

                string message;

                switch (warning.Warning)
                {
                    case Warnings.DownloadError:
                        message = location + "\n\nUnable to download the requested file";
                        break;
                    case Warnings.NotFound:
                        message = location + "\n\nThe requested file has not yet been downloaded";
                        break;
                    case Warnings.Repeated:
                        message = location + "\n\nThe file has been requested multiple times and may be corrupt. Restart GW2 to try again or delete the file.\n\nDo you want to delete the file?";
                        if (MessageBox.Show(this, message, "Details", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == System.Windows.Forms.DialogResult.Yes)
                        {
                            string path = Path.Combine(formMain.PATH_CACHE, Util.FileName.FromAssetRequest(location));
                            try
                            {
                                if (File.Exists(path))
                                    File.Delete(path);
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(this, path + "\n\nUnable to delete the file:\n\n" + ex.Message, "Failed to delete", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                        return;
                    case Warnings.Uncompressed:
                        message = location + "\n\nThe requested file is a uncompressed fallback. Download the compressed file instead and restart GW2.";
                        break;
                    default:
                        return;
                }

                if (data is Exception)
                {
                    message += "\n\n" + ((Exception)data).Message;
                }

                MessageBox.Show(this, message, "Details", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}
