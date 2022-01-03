using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Configuration;
using System.Data;
using System.IO;
using System.Text.RegularExpressions;


namespace SQLShell
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		protected string sUndoString = string.Empty;

		public MainWindow()
		{
			InitializeComponent();
			txtTableName.Text = ConfigurationManager.AppSettings.Get("initTableName");
		}

		#region Button Clicks
		private void btnConvert_Click(object sender, RoutedEventArgs e)
		{
			if ((bool)cbxPattern.IsChecked) DoPatternConversion();
			else GetConvertedList();
		}
		#endregion

		#region KeyPress and Drag Events
		private void txtList_KeyDown(object sender, KeyEventArgs e)
		{
			SetNumLines();
		}

		private void textBox1_PreviewDragOver(object sender, DragEventArgs e)
		{
			e.Effects = DragDropEffects.All;
			e.Handled = true;
		}

		private void Grid_Drop(object sender, DragEventArgs e)
		{
			if (e.Data.GetDataPresent(DataFormats.FileDrop))
			{
				string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
				OpenFile(System.IO.Path.GetFullPath(files[0].ToString()));
			}
		}
		#endregion

		private void GetConvertedList()
		{
			bool bUseSingleQuote = (bool)cbxUseSingleQuote.IsChecked;
			bool bGenInString = (bool)cbxGenerateString.IsChecked;
			bool bGenValuesString = (bool)cbxGenerateValuesString.IsChecked;
			string sNewText = string.Empty;

			List<string> list = new List<string>(Regex.Split(txtList.Text, Environment.NewLine));
			list = list.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
			int lineCount = list.Count;

			if (!bGenInString && !bGenValuesString)
			{
				list = list.Select(s =>
										(lineCount >= 1000 ? "INSERT INTO " + txtTableName.Text.Trim() + " VALUES " : "")
										+ (lineCount < 1000 ? ", " : "")
										+ "("
										+ (bUseSingleQuote ? "'" : "")
										+ s
										+ (bUseSingleQuote ? "'" : "")
										+ ")"
									).ToList();

				if (lineCount < 1000)
				{
					list[0] = "VALUES " + list[0].Remove(0, 1).Trim();
					list.Insert(0, "INSERT INTO " + txtTableName.Text.Trim());
				}

				sNewText = String.Join(Environment.NewLine, list.ToArray());
			}
			else
			{
				list = list.Select(s => "," + (bGenValuesString ? "(":"") + (bUseSingleQuote ? "'" : "") + s + (bUseSingleQuote ? "'" : "") + (bGenValuesString ? ")" : "")).ToList();
				list[0] = list[0].Remove(0, 1).Trim();

				sNewText = (bGenInString ? "(" : "") + String.Join("",list.ToArray()) + (bGenInString ? ")" : "");
			}

			SystemSet(sNewText);
		}

		private void OpenFile(string sFileName)
		{
			StringBuilder sb = new StringBuilder();
			StringBuilder sbInclude = new StringBuilder();
			string sLine = string.Empty;

			textBox1.Text = sFileName;
			txtTableName.Text = txtTableName.Text.Replace("@", "#");

			string sSplitValue = string.Empty;
			if (rbOpenCSVtoTable.IsChecked == true) sSplitValue = ",";
			if (rbPipeDelimited.IsChecked == true) sSplitValue = "|";
			if (rbTabDelimited.IsChecked == true) sSplitValue = "\t";
			if (rbDoubleQuoteCSVDelimited.IsChecked == true) sSplitValue = "\",\"";

			if (File.Exists(sFileName))
			{
				StreamReader sReader = null;
				try
				{
					sReader = new StreamReader(sFileName);

					// Get the first line to create the file.
					sLine = sReader.ReadLine();
					string sAdjustFileName = sFileName.Replace("F:\\", "\\\\bioserver\\data\\");
					sAdjustFileName = sFileName.Replace("J:\\", "\\\\bioserver\\data\\development\\csv\\");

					if (sLine.Length > 0)
					{
						sb.Append("IF OBJECT_ID('tempdb.." + txtTableName.Text + "') IS NOT NULL DROP TABLE " + txtTableName.Text + ";");
						sb.Append(Environment.NewLine);
						sb.Append("CREATE TABLE ");
						sb.Append(txtTableName.Text);
						sb.Append(" ( ");

						List<string> list = new List<string>(Regex.Split(sLine, sSplitValue));
						if (rbDoubleQuoteCSVDelimited.IsChecked == true)
						{
							list = list.Select(s => s.Replace("\"", "")).ToList();
							sbInclude.Append(Environment.NewLine + Environment.NewLine);
							sbInclude.Append("UPDATE " + txtTableName.Text.Trim() + " SET [" + list[0].ToString().Trim() + "] = SUBSTRING([" + list[0].ToString().Trim() + "],2,DATALENGTH([" + list[0].ToString().Trim() + "])-1)");
							sbInclude.Append(Environment.NewLine);
							sbInclude.Append(", [" + list[list.Count - 1].ToString().Trim() + "] = SUBSTRING([" + list[list.Count - 1].ToString().Trim() + "], 1, DATALENGTH([" + list[list.Count - 1].ToString().Trim() + "]) - 1); ");
						}

						list = list.Select(s => ", [" + s + "] VARCHAR(100) NULL").ToList();
						list[0] = list[0].Remove(0, 1).Trim();
						sb.Append(String.Join(Environment.NewLine, list.ToArray()));

						sb.Append(" ); " + Environment.NewLine + Environment.NewLine);
						sb.Append("BULK INSERT ");
						sb.Append(txtTableName.Text.Trim());
						sb.Append(Environment.NewLine);
						sb.Append("FROM '" + sAdjustFileName + "'");
						sb.Append(Environment.NewLine);
						sb.Append("WITH ( FIRSTROW = 2, FIELDTERMINATOR = '" + sSplitValue + "', ROWTERMINATOR = '\\n' );");

						if (rbDoubleQuoteCSVDelimited.IsChecked == true)
						{
							sb.Append(sbInclude.ToString());
						}

						sb.Append(Environment.NewLine + Environment.NewLine + "SELECT * FROM " + txtTableName.Text.Trim() + ";");

						sb.Append(Environment.NewLine + Environment.NewLine + "--DROP TABLE " + txtTableName.Text.Trim() + ";");
					}
				}
				finally
				{
					if (sReader != null)
						sReader.Close();
				}
				SystemSet(sb.ToString());
			}
		}

		#region Main Textbox Manipulation
		private void AddValueToString(bool bPre, bool bPost, string sValue)
		{
			List<string> list = new List<string>(Regex.Split(txtList.Text, Environment.NewLine)).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
			//list = list.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
			list = list.Select(s => (bPre ? sValue : "") + s + (bPost ? sValue : "")).ToList();
			string sNewText = String.Join(Environment.NewLine, list.ToArray());

			SystemSet(sNewText);
		}

		private void SystemSet(string sNewText)
		{
			sUndoString = txtList.Text;
			txtList.Clear();
			txtList.Text = sNewText;
			SetNumLines();
			Clipboard.SetDataObject(sNewText, true);
		}

		private void SetNumLines()
		{
			txtNumLines.Text = "Num Lines: " + txtList.LineCount.ToString();
		}

		private void ReplaceValueInString(string sValueToReplace, string sReplaceValueWithThis)
		{
			List<string> list = new List<string>(Regex.Split(txtList.Text, Environment.NewLine));
			list = list.Select(s => s.Replace(sValueToReplace, sReplaceValueWithThis)).ToList();
			string sNewText = String.Join(Environment.NewLine, list.ToArray());
			SystemSet(sNewText);
		}
		#endregion

		#region AddToStart
		private void btnAddCommaStart_Click(object sender, RoutedEventArgs e)
		{
			AddValueToString(true, false, ",");
		}

		private void btnAddSingleQuoteStart_Click(object sender, RoutedEventArgs e)
		{
			AddValueToString(true, false, "'");
		}

		private void btnAddDoubleQuoteStart_Click(object sender, RoutedEventArgs e)
		{
			AddValueToString(true, false, "\"");
		}

		private void btnAddTextLeft_Click(object sender, RoutedEventArgs e)
		{
			if (txtPre.Text.Length > 0)
			{
				AddValueToString(true, false, txtPre.Text);
			}
		}
		#endregion

		#region AddToEnd
		private void btnAddSemicolonEnd_Click(object sender, RoutedEventArgs e)
		{
			AddValueToString(false, true, ";");
		}

		private void btnAddSingleQuoteEnd_Click(object sender, RoutedEventArgs e)
		{
			AddValueToString(false, true, "'");
		}

		private void btnAddDoubleQuoteEnd_Click(object sender, RoutedEventArgs e)
		{
			AddValueToString(false, true, "\"");
		}

		private void btnAddTextRight_Click(object sender, RoutedEventArgs e)
		{
			if (txtPost.Text.Length > 0)
			{
				AddValueToString(false, true, txtPost.Text);
			}
		}
		#endregion

		#region BeginAndEndString
		private void btnAddTextLeftAndRight_Click(object sender, RoutedEventArgs e)
		{
			if (txtPre.Text.Length > 0 && txtPost.Text.Length > 0)
			{
				AddValueToString(true, false, txtPre.Text);
				AddValueToString(false, true, txtPost.Text);
			}
		}

		private void btnAddParenthesis_Click(object sender, RoutedEventArgs e)
		{
			AddValueToString(true, false, "(");
			AddValueToString(false, true, ")");
		}

		private void btnAddSquareBrackets_Click(object sender, RoutedEventArgs e)
		{
			AddValueToString(true, false, "[");
			AddValueToString(false, true, "]");
		}

		private void btnAddSingleQuotes_Click(object sender, RoutedEventArgs e)
		{
			AddValueToString(true, true, "'");
		}

		private void btnAddDoubleQuotes_Click(object sender, RoutedEventArgs e)
		{
			AddValueToString(true, true, "\"");
		}

		private void btnGenRange_Click(object sender, RoutedEventArgs e)
		{
			int iMinValue;
			int iMaxValue;
			if (txtPre.Text.Length > 0 && txtPost.Text.Length > 0 && (int.TryParse(txtPre.Text, out iMinValue)) && (int.TryParse(txtPost.Text, out iMaxValue)))
			{
				List<string> list = new List<string>();
				for (int i = iMaxValue; i >= iMinValue; i--)
				{
					list.Insert(0, i.ToString());
				}
				string sNewText = String.Join(Environment.NewLine, list.ToArray());
				SystemSet(sNewText);
			}
		}
		#endregion

		#region RemovalButtons
		private void btnRemoveLineFeeds_Click(object sender, RoutedEventArgs e)
		{
			string sListText = txtList.Text.Replace(Environment.NewLine, "");
			SystemSet(sListText);
		}

		private void btnRemoveTabs_Click(object sender, RoutedEventArgs e)
		{
			ReplaceValueInString("\t", "");
		}

		private void btnReplaceTabA_Click(object sender, RoutedEventArgs e)
		{
			string sListText = txtList.Text.Replace("\t", Environment.NewLine);
			SystemSet(sListText);
		}

		private void btnReplaceTabB_Click(object sender, RoutedEventArgs e)
		{
			ReplaceValueInString("\t", "','");
		}

		private void btnReplaceWithTab_Click(object sender, RoutedEventArgs e)
		{
			if (txtPre.Text.Length > 0)
			{
				string sListText = txtList.Text.Replace(txtPre.Text, "\t");
				SystemSet(sListText);
			}
		}

		private void btnReplaceTabD_Click(object sender, RoutedEventArgs e)
		{
			if (txtPost.Text.Length > 0)
			{
				ReplaceValueInString("\t", txtPost.Text);
			}
		}

		private void btnRemoveVal_Click(object sender, RoutedEventArgs e)
		{
			if (txtPost.Text.Length > 0)
			{
				ReplaceValueInString(txtPost.Text, "");
			}
		}

		private void btnReplace_Click(object sender, RoutedEventArgs e)
		{
			ReplaceValueInString(txtPre.Text, txtPost.Text);
		}

		private void btnTrimAll_Click(object sender, RoutedEventArgs e)
		{
			List<string> list = new List<string>(Regex.Split(txtList.Text, Environment.NewLine));
			list = list.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
			list = list.Select(s => s.Trim()).ToList();
			string sNewText = String.Join(Environment.NewLine, list.ToArray());
			SystemSet(sNewText);
		}

		private void btnRemoveFirstValue_Click(object sender, RoutedEventArgs e)
		{
			List<string> list = new List<string>(Regex.Split(txtList.Text, Environment.NewLine));
			list = list.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
			list = list.Select(s => s.Remove(0, 1)).ToList();
			string sNewText = String.Join(Environment.NewLine, list.ToArray());
			SystemSet(sNewText);
		}

		private void btnRegexReplace_Click(object sender, RoutedEventArgs e)
		{
			List<string> list = new List<string>(Regex.Split(txtList.Text, Environment.NewLine));
			list = list.Select(s => Regex.Replace(s, txtPre.Text, txtPost.Text)).ToList();
			string sNewText = String.Join(Environment.NewLine, list.ToArray());
			SystemSet(sNewText);
		}
		#endregion

		private void btnUndo_Click(object sender, RoutedEventArgs e)
		{
			if (sUndoString.Length > 0)
			{
				txtList.Clear();
				txtList.Text = sUndoString;
				Clipboard.SetDataObject(sUndoString, true);
			}
		}

		private void btnReplaceTabs_Click(object sender, RoutedEventArgs e)
		{
			string sTab = "\t";
			List<string> list = new List<string>(Regex.Split(txtList.Text, Environment.NewLine)).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
			//list = list.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
			list = list.Where(s => s.IndexOf(sTab) > 0).Select(s => s.Remove(s.IndexOf(sTab), sTab.Length).Insert(s.IndexOf(sTab), txtPre.Text)).ToList();
			list = list.Where(s => s.IndexOf(sTab) > 0).Select(s => s.Remove(s.IndexOf(sTab), sTab.Length).Insert(s.IndexOf(sTab), txtPost.Text)).ToList();

			string sNewText = String.Join(Environment.NewLine, list.ToArray());
			SystemSet(sNewText);
		}

		private void btnClearAll_Click(object sender, RoutedEventArgs e)
		{
			txtList.Clear();
			SetNumLines();
			Clipboard.SetDataObject(String.Empty, true);
		}

		private void DoPatternConversion()
		{
			string sDelimiter = txtDelimiter.Text;
			StringBuilder sb = new StringBuilder();
			List<string> list = new List<string>(Regex.Split(txtList.Text, Environment.NewLine));
			int nCount = 0;

			foreach (string s in list)
			{
				List<string> list2 = new List<string>(Regex.Split(s,sDelimiter));

				nCount = list2.Count();

				string sNew = txtPattern.Text;

				for (int i = 1; i <= nCount; i++) {
					sNew = sNew.Replace("<%" + i.ToString() + ">", list2.ToArray()[i - 1].ToString());
				}

				sb.Append(sNew + Environment.NewLine);
			}

			SystemSet(sb.ToString());

		}

		private void btnReplaceWithLineFeed_Click(object sender, RoutedEventArgs e)
		{
			if (txtPre.Text.Length > 0)
			{
				string sListText = txtList.Text.Replace(txtPre.Text, Environment.NewLine);
				SystemSet(sListText);
			}
		}

		private void btnStripToLeft_Click(object sender, RoutedEventArgs e)
		{
			List<string> list = new List<string>(Regex.Split(txtList.Text, Environment.NewLine));
			list = list.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
			list = list.Select(s => s.Trim()).ToList();
			list = list.Select(s => s.Substring(s.IndexOf(txtPre.Text)+1)).ToList();
			string sNewText = String.Join(Environment.NewLine, list.ToArray());
			SystemSet(sNewText);
		}

		private void btnStripToRight_Click(object sender, RoutedEventArgs e)
		{
			List<string> list = new List<string>(Regex.Split(txtList.Text, Environment.NewLine));
			list = list.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
			list = list.Select(s => s.Trim()).ToList();
			list = list.Select(s => s.IndexOf(txtPost.Text) > -1 ? s.Remove(s.IndexOf(txtPost.Text)) : s).ToList();
			string sNewText = String.Join(Environment.NewLine, list.ToArray());
			SystemSet(sNewText);
		}

		private void btnReplaceLineFeed_Click(object sender, RoutedEventArgs e)
		{
			if (txtPost.Text.Length > 0)
			{
				string sListText = txtList.Text.Replace(Environment.NewLine, txtPost.Text);
				SystemSet(sListText);
			}
		}
	}
}

