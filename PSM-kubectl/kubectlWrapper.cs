using System;
using System.IO;
using System.Diagnostics;
using System.Linq;

namespace kube
{
    class wrapper
    {
        static string RandomString(int length)
        {
            Random random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        }
        static void InitKubeConfig(string kcName)
        {
            string targetUsername = Environment.GetEnvironmentVariable("TARGETUSERNAME");
            string targetPassword = Environment.GetEnvironmentVariable("TARGETPASSWORD");
            string targetAddress = Environment.GetEnvironmentVariable("TARGETADDRESS");
            string kubectlDirectory = Environment.GetEnvironmentVariable("KUBECTLDIRECTORY");

            Environment.SetEnvironmentVariable("PATH", Environment.GetEnvironmentVariable("PATH") + ";" + kubectlDirectory);
            Environment.SetEnvironmentVariable("KUBECONFIG", kcName + ".config");
            if (targetPassword.Contains("."))
            {
                Process createKubeConfig = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd",
                        Arguments = "/c kubectl config set-cluster kubernetes --server=" + targetAddress +
                                    "&& kubectl config set-cluster kubernetes --insecure-skip-tls-verify=true" +
                                    "&& kubectl config set-context " + targetUsername + "@kubernetes --cluster=kubernetes --user=" + targetUsername +
                                    "&& kubectl config set-credentials " + targetUsername + " --token=" + targetPassword +
                                    "&& kubectl config use-context " + targetUsername + "@kubernetes",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                createKubeConfig.Start();
            }
            else
            {
                string targetPasswordDecoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(targetPassword));
                if (targetPasswordDecoded.Contains("kind: Config"))
                {
                    File.WriteAllText(kcName + ".config", targetPasswordDecoded);
                }
                else
                {
                    Console.WriteLine("Invalid credentials");
                    throw new Exception("Invalid credentials");
                }
            }
        }
        static void InvalidInput()
        {
            Console.WriteLine("Invalid input");
            Console.Write("\n> ");
        }
        static void Main()
        {
            string kcName = RandomString(32);
            InitKubeConfig(kcName);
            Console.WriteLine("CyberArk PSM Connector for kubectl\n(c) CyberArk Software Ltd. All rights reserved.");
            Console.Write("\n> ");
            string input = null;
            Process kubectlRunner = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd",
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs args)
            {
                args.Cancel = true;
                if (!(kubectlRunner == null))
                {
                    Process taskkill = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = "/F /T /PID " + kubectlRunner.Id,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    if (File.Exists(kcName + ".config")) File.Delete(kcName + ".config");
                    taskkill.Start();
                }
                else return;
            };
            while (!(input == "exit"))
            {
                input = Console.ReadLine();
                switch (input)
                {
                    case null:
                        if (File.Exists(kcName + ".config")) File.Delete(kcName + ".config");
                        return;
                    case "exit":
                        if (File.Exists(kcName + ".config")) File.Delete(kcName + ".config");
                        break;
                    case "":
                        Console.Write("> ");
                        break;
                    case var _ when input.Equals("cls") || input.Equals("clear"):
                        Console.Clear();
                        Console.Write("\n> ");
                        break;
                    case var _ when input.Contains("&&") || input.Contains("|") || input.Contains("<") || input.Contains(">"):
                        InvalidInput();
                        break;
                    case var _ when input.StartsWith("kubectl"):
                        kubectlRunner.StartInfo.Arguments = "/c " + input;
                        kubectlRunner.Start();
                        while (!kubectlRunner.StandardOutput.EndOfStream)
                        {
                            string output = kubectlRunner.StandardOutput.ReadLine();
                            Console.WriteLine(output);
                        }
                        while (!kubectlRunner.StandardError.EndOfStream)
                        {
                            string error = kubectlRunner.StandardError.ReadLine();
                            Console.WriteLine(error);
                        }
                        Console.Write("\n> ");
                        break;
                    default:
                        InvalidInput();
                        break;
                }
            }
        }
    }
}
