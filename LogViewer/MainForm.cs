using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace LogViewer
{
    public partial class MainForm : Form
    {
        #region Fields

        LogFile logFile;

        #endregion

        #region Ctor

        public MainForm()
        {
            InitializeComponent();
            searchCountToolStripLabel.Text = string.Empty;
            viewerSplitContainer.Panel2Collapsed = true;
            searchVisibleToolStripMenuItem.Checked = false;
            searchResultsDataGridView.AutoGenerateColumns = false;
        }

        #endregion

        #region Event Handlers

        private void homeButton_Click(object sender, EventArgs e)
        {
            try
            {
                logFile.CurrentPage = 1;
                LoadPage(logFile.ReadOnePage());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void endButton_Click(object sender, EventArgs e)
        {
            try
            {
                logFile.CurrentPage = logFile.PageCount;
                LoadPage(logFile.ReadOnePage());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void previousPageButton_Click(object sender, EventArgs e)
        {
            RunSafely(() =>
            {
                LoadPage(logFile.ReadPreviousPage());
            });
        }

        private void nextPageButton_Click(object sender, EventArgs e)
        {
            try
            {
                LoadPage(logFile.ReadNextPage());
            }
            catch (EndOfStreamException)
            {
                // last page reached, do nothing
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            RunSafely(() =>
            {
                if (logFile != null)
                {
                    logFile.Dispose();
                }
            });
        }

        private void gotoPageButton_Click(object sender, EventArgs e)
        {
            RunSafely(() =>
            {
                int pageNo = (int)pageNoNumericUpDown.Value;
                if (logFile != null && pageNo > 0 && pageNo <= logFile.PageCount)
                {
                    logFile.CurrentPage = pageNo;
                    LoadPage(logFile.ReadOnePage());
                }
            });
        }

        private void searchButton_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            RunSafely(() =>
            {
                ResetContentBackColor();
                if (logFile != null)
                {
                    var results = logFile.SearchWithinFile(searchPatternTextBox.Text, regexCheckBox.Checked, caseSensitiveCheckBox.Checked);
                    searchResultsDataGridView.DataSource = results;
                    searchCountToolStripLabel.Text = results.Count.ToString() + " results";
                }
            });
            Cursor.Current = Cursors.Default;
        }

        private void searchResultsDataGridView_DoubleClick(object sender, EventArgs e)
        {
            RunSafely(() =>
            {
                if (searchResultsDataGridView.SelectedRows.Count > 0)
                {
                    var results = (List<SearchResult>)searchResultsDataGridView.DataSource;
                    var index = searchResultsDataGridView.SelectedRows[0].Index;
                    var selectedResult = results[index];
                    var page = selectedResult.PageNo;
                    logFile.CurrentPage = page;
                    LoadPage(logFile.ReadOnePage());
                    pageNoNumericUpDown.Value = logFile.CurrentPage;
                    foreach (var result in FindAllResultsInThePage(results, index))
                    {
                        SetResultBackColor(result);
                    }
                }
            });
        }

        private void searchPatternTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                searchButton.Focus();
                searchButton.PerformClick();
            }
        }

        private void pageNoNumericUpDown_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                gotoPageButton.PerformClick();
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            RunSafely(() =>
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.CheckFileExists = true;
                    ofd.Multiselect = false;
                    var result = ofd.ShowDialog();
                    if (result == System.Windows.Forms.DialogResult.OK)
                    {
                        OpenLogFile(ofd.FileName);
                    }
                }
            });
        }

        private void searchVisibleToolStripMenuItem_CheckedChanged(object sender, EventArgs e)
        {
            viewerSplitContainer.Panel2Collapsed = !searchVisibleToolStripMenuItem.Checked;
            if (!viewerSplitContainer.Panel2Collapsed)
            {
                searchPatternTextBox.Focus();
                searchPatternTextBox.SelectAll();
            }
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.PageDown)
            {
                nextPageButton.PerformClick();
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.PageUp)
            {
                previousPageButton.PerformClick();
                e.Handled = true;
            }
        }

        #endregion

        #region Private Methods

        private void RunSafely(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void OpenLogFile(string filePath)
        {
            try
            {
                ResetForm();
                logFile = new LogFile(filePath);
                pageCountTextBox.Text = logFile.PageCount.ToString();
                pageNoNumericUpDown.Maximum = logFile.PageCount;
                LoadPage(logFile.ReadOnePage());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private void ResetForm()
        {
            if (logFile != null)
            {
                logFile.Dispose();
            }
            logFile = null;
            searchResultsDataGridView.DataSource = null;
            searchPatternTextBox.Text = string.Empty;
            searchCountToolStripLabel.Text = string.Empty;
            LoadPage(string.Empty);
        }

        private void LoadPage(string text)
        {
            contentRichTextBox.Text = text;
            // set richtextbox length to the new text's length
            contentRichTextBox.RightMargin = TextRenderer.MeasureText(contentRichTextBox.Text, contentRichTextBox.Font).Width;
            // set caret position to the top
            contentRichTextBox.SelectionLength = 0;
            contentRichTextBox.SelectionStart = 0;
            if (logFile != null && !logFile.IsDisposed)
            {
                pageNoNumericUpDown.Value = logFile.CurrentPage;
            }
        }

        private void ResetContentBackColor()
        {
            contentRichTextBox.SelectAll();
            contentRichTextBox.SelectionBackColor = contentRichTextBox.BackColor;
            contentRichTextBox.DeselectAll();
        }

        private void SetResultBackColor(SearchResult result)
        {
            contentRichTextBox.SelectionStart = result.Index;
            contentRichTextBox.SelectionLength = result.Length;
            contentRichTextBox.SelectionBackColor = Color.Yellow;
            contentRichTextBox.SelectionLength = 0;
        }

        private List<SearchResult> FindAllResultsInThePage(List<SearchResult> results, int index)
        {
            List<SearchResult> samePageResults = new List<SearchResult>();
            samePageResults.Add(results[index]);
            var page = results[index].PageNo;
            for (int i = index + 1; i < results.Count && results[i].PageNo == page; i++)
            {
                samePageResults.Add(results[i]);
            }
            for (int i = index - 1; i >= 0 && results[i].PageNo == page; i--)
            {
                samePageResults.Add(results[i]);
            }
            return samePageResults;
        }

        #endregion

        private void MainForm_Load(object sender, EventArgs e)
        {
            Application.DoEvents();
            openToolStripMenuItem.PerformClick();
            // Get the window to the front.
            this.TopMost = true;
            this.TopMost = false;

            // 'Steal' the focus.
            this.Activate();
        }
    }
}
