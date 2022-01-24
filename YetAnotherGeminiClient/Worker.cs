using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.IO;

namespace YetAnotherGeminiClient
{
    public class Worker
    {
        public string Destination = "";
        public Uri Uri;
        public string meta;
        public string Output = "";
        public DocumentType Type = DocumentType.SYSTEM;
        public DocumentState State = DocumentState.ERROR;

        public event EventHandler OnNavigate;
        public event EventHandler OnSuccess;
        public event EventHandler OnError;

        public TcpClient Client;
        public Thread Thread;

        public void Navigate(string url)
        {
            Uri = new Uri(url);
            Type = DocumentType.SYSTEM;
            Destination = url;
            if (Client != null)
            {
                Client?.Close();
            }

            Thread = new Thread(() =>
            {
                try
                {
                    if (OnNavigate != null) OnNavigate(this, null);

                    if (Uri.Scheme == "gemini")
                    {
                        int port = 1965;
                        if (Uri.Port >= 0) port = Uri.Port;

                        Client = new TcpClient(Uri.Host, port);

                        using (SslStream stream = new SslStream(Client.GetStream(), false,
                            new RemoteCertificateValidationCallback(HandleServerCertificate), null))
                        {
                            stream.AuthenticateAsClient(Uri.Host, null, System.Security.Authentication.SslProtocols.Tls12, true);
                            stream.Write(Encoding.UTF8.GetBytes(Uri.AbsoluteUri + "\r\n"));

                            Output = ReadMessage(stream);
                        }

                        string header = Output.Substring(0, Output.IndexOf("\r\n"));
                        short status = Int16.Parse(header.Substring(0, header.IndexOf(" ")));
                        Output = Output.Substring(Output.IndexOf("\r\n") + 1).Trim();
                        Console.WriteLine("\"" + status + "\"");
                        if (status == 20)
                        {
                            State = DocumentState.OK;
                            Type = DocumentType.GEMINI;
                            if (OnSuccess != null) OnSuccess(this, null);
                        }
                        else if (status == 10 || status == 11)
                        {
                            State = DocumentState.INPUT_REQUESTED;
                            Type = DocumentType.GEMINI;
                            Output = 
                                "# Awaiting user input...\r\n\r\n" +
                                "This page is asking for your input. Enter yours below, then press Enter to continue.\r\n\r\n" +
                                "> GEMINI STATUS " + status + "\r\n" +
                                "> " + header.Substring(2).Trim() + "\r\n";
                            if (OnSuccess != null) OnSuccess(this, null);
                        }
                        else
                        {
                            switch (status)
                            {
                                case 40:
                                    State = DocumentState.TEMPORARY_ERROR;
                                    Output =
                                        "# An error happened\r\n\r\n" +
                                        "There was an unspecified error preventing you from accessing this page. There might be some useful information below.";
                                    break;
                                case 41:
                                    State = DocumentState.SERVER_UNAVAILABLE;
                                    Output =
                                        "# Server unavailable\r\n\r\n" +
                                        "Looks like the server doesn't feel like responding right now. Maybe try again later?";
                                    break;
                                case 42:
                                    State = DocumentState.CGI_ERROR;
                                    Output =
                                        "# CGI error\r\n\r\n" +
                                        "The server made a mistake while trying to process the content for you. A reload might do the trick.";
                                    break;
                                case 43:
                                    State = DocumentState.PROXY_ERROR;
                                    Output =
                                        "# Proxy error\r\n\r\n" +
                                        "The server made a mistake while trying to convert the content you requested to a useful one. A reload might do the trick.";
                                    break;
                                case 44:
                                    int time = -1;
                                    int.TryParse(header.Substring(2).Trim(), out time);
                                    State = DocumentState.RATE_LIMITED;
                                    Output =
                                        "# You're being rate-limited\r\n\r\n" +
                                        "The server would like you not to send another request for " + time + " seconds. Maybe the server is being mad at you?";
                                    break;
                                case 50:
                                    State = DocumentState.PERMANENT_ERROR;
                                    Output =
                                        "# An error happened\r\n\r\n" +
                                        "There was an unspecified error preventing you from accessing this page. There might be some useful information below.";
                                    break;
                                case 51:
                                    State = DocumentState.NOT_FOUND;
                                    Output =
                                        "# Not found\r\n\r\n" +
                                        "There wasn't anything here. Lost, gone or wrong address?";
                                    break;
                                case 52:
                                    State = DocumentState.GONE;
                                    Output =
                                        "# Gone\r\n\r\n" +
                                        "There's no longer anything here. Everything was lost.";
                                    break;
                                case 53:
                                    State = DocumentState.PROXY_UNAVAILABLE;
                                    Output =
                                        "# Proxy unavailable\r\n\r\n" +
                                        "Sorry user, but the content you're looking for is in another server/protocol.";
                                    break;
                                case 59:
                                    State = DocumentState.BAD_REQUEST;
                                    Output =
                                        "# Bad request\r\n\r\n" +
                                        "Your request could not be parsed by the server. Could you recheck the address for any typos?";
                                    break;
                                default:
                                    State = DocumentState.BAD_HEADER;
                                    Output =
                                        "# Unknown status code\r\n\r\n" +
                                        "The server responded with a status code which YAGC did not know. There might be some useful information below.";
                                    break;
                            }
                            Type = DocumentType.GEMINI;
                            Output += "\r\n\r\n" +
                                "> GEMINI STATUS " + status + "\r\n" +
                                "> " + header.Substring(2).Trim();
                            if (OnError != null) OnError(this, null);
                        }

                        Client.Close();

                    }
                    else if (Uri.Scheme == "gopher")
                    {
                        int port = 70;
                        if (Uri.Port >= 0) port = Uri.Port;

                        Client = new TcpClient(Uri.Host, port);
                        NetworkStream stream = Client.GetStream();

                        Match type = new Regex(@"^\/?(.?)([^\?]*)\??(.*)").Match(Uri.LocalPath);

                        byte[] data = Encoding.UTF8.GetBytes(type.Groups[2].Value + (type.Groups[3].Value == "" ? "" : "\t" + type.Groups[3].Value) + "\r\n");
                        stream.Write(data, 0, data.Length);

                        Output = ReadMessage(stream);
                        Type = type.Groups[1].Value == "" || type.Groups[1].Value == "1" || type.Groups[1].Value == "7"
                            ? DocumentType.GOPHER : DocumentType.TEXT;

                        Client.Close();

                        if (OnSuccess != null) OnSuccess(this, null);
                    }
                    else
                    {
                    }
                }
                catch (Exception e)
                {
                    if (Client == null) return;
                    if (OnError != null) OnError(this, new UnhandledExceptionEventArgs(e, false));
                }
            });
            Thread.Start();
        }

        string ReadMessage(Stream stream)
        {
            byte[] buffer = new byte[16384];
            StringBuilder message = new StringBuilder();
            int bytes = -1;
            while (bytes != 0)
            {
                bytes = stream.Read(buffer, 0, buffer.Length);
                Decoder decoder = Encoding.UTF8.GetDecoder();
                char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                decoder.GetChars(buffer, 0, bytes, chars, 0);
                message.Append(chars);
                if (message.ToString().IndexOf("<EOF>") >= 0) break;
            }
            return message.ToString();
        }

        bool HandleServerCertificate(object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            X509Certificate2 cert2 = new X509Certificate2(certificate);
            return true;
            // return cert2.GetNameInfo(X509NameType.DnsName, false).ToLower() == Uri.Host.ToLower();
        }
    }

    public enum DocumentType
    {
        SYSTEM,
        GEMINI,
        GOPHER,
        TEXT,
    }

    public enum DocumentState
    {
        OK,
        INPUT_REQUESTED,

        ERROR,

        BAD_HEADER,

        TEMPORARY_ERROR,
        SERVER_UNAVAILABLE,
        CGI_ERROR,
        PROXY_ERROR,
        RATE_LIMITED,

        PERMANENT_ERROR,
        NOT_FOUND,
        GONE,
        PROXY_UNAVAILABLE,
        BAD_REQUEST,
    }
}
