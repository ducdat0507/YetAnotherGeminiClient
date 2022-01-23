using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace YetAnotherGeminiClient
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public List<Tab> Tabs = new List<Tab>();
        public int CurrentTab;

        ScrollViewer DocumentScrollViewer;

        public MainWindow()
        {
            InitializeComponent(); 
            DocumentScrollViewer = (ScrollViewer)DocumentViewer.Template.FindName("PART_ContentHost", DocumentViewer);
            CreateNewTab("gemini://gemini.circumlunar.space");
        }

        public void CreateNewTab(string url = "about://home")
        {
            Tab newTab = new Tab()
            {
                Worker = new Worker(),
                TabButton = new Button()
                {
                    Content = "New Tab",
                    Style = (Style)Resources["TabButton"],
                },
            };
            newTab.Worker.OnSuccess += (e, args) =>
            {
                Document.Dispatcher.Invoke(() =>
                {
                    if (Tabs[CurrentTab] == newTab)
                    {
                        newTab.ScrollOffset = 0;
                        UpdateDocument();
                    }
                });
            };
            newTab.Worker.OnError += (e, args) =>
            {
                Document.Dispatcher.Invoke(() =>
                {
                    if (Tabs[CurrentTab] == newTab)
                    {
                        newTab.ScrollOffset = 0;
                        UpdateDocument();
                    }
                });
            };
            newTab.TabButton.Click += (e, args) =>
            {
                Document.Dispatcher.Invoke(() =>
                {
                    SetTab(Tabs.IndexOf(newTab));
                });
            };

            newTab.Navigate("gemini://gemini.circumlunar.space");
            Tabs.Add(newTab);
            TabsHolder.Children.Add(newTab.TabButton);
            newTab.TabButton.Tag = "active";
            SetTab(Tabs.Count - 1);
        }

        public void SetTab(int id)
        {
            Tab current = Tabs[CurrentTab];
            Tab next = Tabs[id];
            if (current != null) current.TabButton.Tag = "";
            next.TabButton.Tag = "active";
            CurrentTab = id;
            UpdateDocument();
        }

        public void CloseTab(int id)
        {
            TabsHolder.Children.RemoveAt(id);
            Tabs.RemoveAt(id);
            if (Tabs.Count == 0)
            {
                Close();
            }
            else if (id == CurrentTab)
            {
                Tabs[CurrentTab].TabButton.Tag = "active";
                UpdateDocument();
            }
            else if (id < CurrentTab)
            {
                CurrentTab--;
            }
        }

        private void UpdateDocument()
        {
            Document.Blocks.Clear();
            ContentTable.Children.Clear();

            Tab current = Tabs[CurrentTab];
            Worker worker = Tabs[CurrentTab].Worker;
            if (current == null || worker == null) return;

            AddressBox.Text = worker.Destination;
            current.TabButton.Content = worker.Uri.Host;
            DomainNameLabel.Text = worker.Uri.Host.Replace(".", "\n.");
            try
            {
                bool isTitleSet = false;

                var lines = worker.Output.Split(new[] { "\n", "\r\n" }, StringSplitOptions.None);

                Paragraph currentParagraph = null;
                List currentList = null;

                if (worker.Type == DocumentType.GEMINI)
                {

                    string mode = "normal";


                    foreach (string line in lines)
                    {
                        if (line.StartsWith("```"))
                        {
                            mode = mode == "pre" ? "normal" : "pre";
                            if (mode == "pre")
                            {
                                currentParagraph = new Paragraph()
                                {
                                    TextAlignment = TextAlignment.Left,
                                    Margin = new Thickness(0),
                                    FontSize = 16,
                                    FontFamily = new FontFamily("Consolas"),
                                };
                                Document.Blocks.Add(currentParagraph);
                            }
                            else
                            {
                                currentParagraph = null;
                            }
                            continue;
                        }

                        if (mode == "pre")
                        {
                            if (currentParagraph.Inlines.Count > 0) currentParagraph.Inlines.Add(new LineBreak());
                            currentParagraph.Inlines.Add(new Run(line));
                            continue;
                        }

                        if (line.StartsWith("*"))
                        {
                            if (mode != "list")
                            {
                                currentList = new List()
                                {
                                    Margin = new Thickness(0),
                                    MarkerOffset = 10,
                                    Padding = new Thickness(25, 0, 0, 0),
                                };
                                Document.Blocks.Add(currentList);
                                mode = "list";
                            }
                            currentParagraph = new Paragraph()
                            {
                                FontSize = 16,
                                Margin = new Thickness(0),
                            };
                            currentList.ListItems.Add(new ListItem(currentParagraph));
                            currentParagraph.Inlines.Add(new Run(line.Substring(1).Trim()));
                            continue;
                        }
                        else if (line.StartsWith(">"))
                        {
                            if (mode != "quote")
                            {
                                currentParagraph = new Paragraph()
                                {
                                    FontSize = 16,
                                    Margin = new Thickness(0),
                                    Padding = new Thickness(25, 0, 0, 0),
                                    FontStyle = FontStyles.Italic,
                                };
                                Document.Blocks.Add(currentParagraph);
                                mode = "quote";
                            }
                            if (currentParagraph.Inlines.Count > 0) currentParagraph.Inlines.Add(new LineBreak());
                            currentParagraph.Inlines.Add(new Run(line.Substring(1).Trim()));
                            continue;
                        }
                        else
                        {
                            mode = "normal";
                            currentParagraph = null;
                        }

                        if (line.StartsWith("###"))
                        {
                            Paragraph c = currentParagraph = new Paragraph()
                            {
                                FontSize = 18,
                                FontWeight = FontWeights.Bold,
                                Margin = new Thickness(0),
                                FontFamily = new FontFamily("Georgia"),
                                TextAlignment = TextAlignment.Left,
                            };
                            Document.Blocks.Add(currentParagraph);
                            currentParagraph.Inlines.Add(new Run(line.Substring(3).Trim()));
                            Button b;
                            ContentTable.Children.Add(b = new Button()
                            {
                                Content = line.Substring(3).Trim(),
                                Padding = new Thickness(24, 5, 10, 5),
                                Style = (Style)Resources["ContentTableButton"],
                            });
                            b.Click += (src, args) =>
                            {
                                (c as FrameworkContentElement)?.BringIntoView();
                            };
                        }
                        else if (line.StartsWith("##"))
                        {
                            Paragraph c = currentParagraph = new Paragraph()
                            {
                                FontSize = 24,
                                FontWeight = FontWeights.Bold,
                                Margin = new Thickness(0),
                                FontFamily = new FontFamily("Georgia"),
                                TextAlignment = TextAlignment.Left,
                            };
                            Document.Blocks.Add(currentParagraph);
                            currentParagraph.Inlines.Add(new Run(line.Substring(2).Trim()));
                            Button b;
                            ContentTable.Children.Add(b = new Button()
                            {
                                Content = line.Substring(2).Trim(),
                                Padding = new Thickness(16, 5, 10, 5),
                                Style = (Style)Resources["ContentTableButton"],
                            });
                            b.Click += (src, args) =>
                            {
                                (c as FrameworkContentElement)?.BringIntoView();
                            };
                        }
                        else if (line.StartsWith("#"))
                        {
                            Paragraph c = currentParagraph = new Paragraph()
                            {
                                FontSize = 36,
                                FontWeight = FontWeights.Bold,
                                Margin = new Thickness(0),
                                FontFamily = new FontFamily("Georgia"),
                                TextAlignment = TextAlignment.Left,
                            };
                            Document.Blocks.Add(currentParagraph);
                            currentParagraph.Inlines.Add(new Run(line.Substring(1).Trim()));
                            Button b;
                            ContentTable.Children.Add(b = new Button()
                            {
                                Content = line.Substring(1).Trim(),
                                Padding = new Thickness(10, 5, 10, 5),
                                Style = (Style)Resources["ContentTableButton"],
                            });
                            b.Click += (src, args) =>
                            {
                                (c as FrameworkContentElement)?.BringIntoView();
                            };

                            if (!isTitleSet)
                            {
                                current.TabButton.Content = line.Substring(1).Trim();
                                isTitleSet = true;
                            }
                        }
                        else if (line.StartsWith("=>"))
                        {
                            string lin = line.Substring(2).Trim();
                            int sep = new Regex(@"\s").Match(lin).Index;
                            Uri uri;
                            bool valid = Uri.TryCreate(worker.Uri, (sep > 0 ? lin.Substring(0, sep) : lin).Trim(), out uri);
                            string text = sep >= 0 ? lin.Substring(sep).Trim() : lin;
                            Hyperlink hp;
                            currentParagraph = new Paragraph()
                            {
                                FontSize = 16,
                                Margin = new Thickness(0),
                                Cursor = Cursors.Hand,
                                IsEnabled = valid,
                            };
                            Document.Blocks.Add(currentParagraph);
                            currentParagraph.Inlines.Add(hp = new Hyperlink(new Run(text)));
                            hp.Click += (src, args) =>
                            {
                                if (uri.Scheme == "gemini" || uri.Scheme == "gopher")
                                {
                                    current.Navigate(uri.AbsoluteUri);
                                    AddressBox.Text = uri.AbsoluteUri;
                                }
                                else
                                {
                                    Process p = new Process();
                                    p.StartInfo.UseShellExecute = true;
                                    p.StartInfo.FileName = uri.AbsoluteUri;
                                    p.Start();
                                }
                            };
                        }
                        else
                        {
                            currentParagraph = new Paragraph()
                            {
                                FontSize = 16,
                                Margin = new Thickness(0),
                            };
                            Document.Blocks.Add(currentParagraph);
                            currentParagraph.Inlines.Add(new Run(line.Trim()));
                        }
                    }
                }
                else if (worker.Type == DocumentType.GOPHER)
                {
                    currentParagraph = new Paragraph()
                    {
                        TextAlignment = TextAlignment.Left,
                        Margin = new Thickness(0),
                        FontSize = 16,
                        FontFamily = new FontFamily("Consolas"),
                    };
                    Document.Blocks.Add(currentParagraph);
                    foreach (string line in lines)
                    {
                        string[] content = line.Split('\t');
                        if (currentParagraph.Inlines.Count > 0) currentParagraph.Inlines.Add(new LineBreak());
                        if (content.Length >= 4) { 
                            currentParagraph.Inlines.Add(new Run(content[0].Substring(1))); 
                        }
                    }
                }

                DocumentScrollViewer = (ScrollViewer)DocumentViewer.Template.FindName("PART_ContentHost", DocumentViewer);
                DocumentScrollViewer.ScrollToVerticalOffset(current.ScrollOffset);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
            }
        }

        private void OnTitleBarDrag(object sender, EventArgs e)
        {
            DragMove();
        }

        private void OnWindowResize(object sender, EventArgs e)
        {
            MainGridClip.Rect = new Rect(0, 0, MainGrid.ActualWidth, MainGrid.ActualHeight);

            double width = MainGrid.ActualWidth;
        }

        private void OnWindowStateChanged(object sender, EventArgs e)
        {
            bool isMaximized = WindowState == WindowState.Maximized;

            MainBorder.Visibility = isMaximized ? Visibility.Collapsed : Visibility.Visible;
            MainGrid.Margin = new Thickness(isMaximized ? 6 : 9);
            MainGridClip.RadiusX = MainGridClip.RadiusY = isMaximized ? 0 : 6; 

            ResizeButton.Content = isMaximized ? "\uE923" : "\uE922";
            ResizeButton.ToolTip = isMaximized ? "Restore" : "Maximize";

            TitleBar.Height = isMaximized ? 30 : 42;
        }

        private void OnCloseButtonClick(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void OnMinimizeButtonClick(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        private void OnResizeButtonClick(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Normal)
                WindowState = WindowState.Maximized;
            else
                WindowState = WindowState.Normal;
        }

        private void OnAddressBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                MainDocument.Focus();
                Tabs[CurrentTab].Worker.Navigate(AddressBox.Text);
            }
        }
        private void OnMenuButtonClick(object sender, RoutedEventArgs e)
        {
            MainMenu.IsOpen = true;
        }

        private void OnNewTabButtonClick(object sender, EventArgs e)
        {
            CreateNewTab();
        }
        private void OnCloseTabButtonClick(object sender, EventArgs e)
        {
            CloseTab(CurrentTab);
        }

        private void OnBackButtonClick(object sender, RoutedEventArgs e)
        {
            Tabs[CurrentTab].GoBack();
            AddressBox.Text = Tabs[CurrentTab].Worker.Uri.AbsoluteUri;
        }
        private void OnForwardButtonClick(object sender, RoutedEventArgs e)
        {
            Tabs[CurrentTab].GoForward();
            AddressBox.Text = Tabs[CurrentTab].Worker.Uri.AbsoluteUri;
        }
        private void OnReloadButtonClick(object sender, RoutedEventArgs e)
        {
            Tabs[CurrentTab].Reload();
            AddressBox.Text = Tabs[CurrentTab].Worker.Uri.AbsoluteUri;
        }

        private void OnDocumentScroll(object sender, ScrollEventArgs e)
        {
            Tabs[CurrentTab].ScrollOffset = DocumentScrollViewer.VerticalOffset;
        }
    }
}
