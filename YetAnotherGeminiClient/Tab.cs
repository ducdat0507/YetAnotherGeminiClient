using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace YetAnotherGeminiClient
{
    public class Tab
    {
        public Worker Worker;
        public Button TabButton;

        public double ScrollOffset;

        public List<string> History = new List<string>();
        public int Current;
        public int Redirects;

        public void Navigate(string url, int redirects = 0)
        {
            if (redirects == 0)
            {
                if (Current != History.Count - 1 && History.Count > 0)
                    History.RemoveRange(Current + 1, History.Count - Current - 1);
                History.Add(url);
                Current = History.Count - 1;
            }
            Redirects = redirects;
            Worker.Navigate(url);
        }

        public bool CanGoBack()
        {
            return History.Count > 0 && Current > 0;
        }
        public void GoBack()
        {
            if (CanGoBack())
            {
                Current--;
                Navigate(History[Current], -1);
            }
        }
        public bool CanGoForward()
        {
            return History.Count > 0 && Current < History.Count - 1;
        }
        public void GoForward()
        {
            if (CanGoForward())
            {
                Current++;
                Navigate(History[Current], -1);
            }
        }
        public void Reload()
        {
            Navigate(History[Current], -1);
        }
    }
}
