using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.IO;

class Program
{
    static void Main()
    {
        StartServer();
    }
    static TcpListener tcpListener;
    static X509Certificate2 certificate;
    static SslStream sslStream;

    static void StartServer()
    {
        string certificateFilePath = "Sertificate\\server.pfx";
        certificate = new X509Certificate2(certificateFilePath, ""); ;
        tcpListener = new TcpListener(IPAddress.Any, 8000);
        try
        {
            tcpListener.Start();
            Console.WriteLine($"Program is working in port 8000.\n");
            while (true)
            {
                TcpClient newClient = tcpListener.AcceptTcpClient();
                if (newClient != null)
                {
                    Thread.Sleep(200);
                    ThreadPool.QueueUserWorkItem(ServeClient, newClient);
                }
                else
                {
                    throw new Exception("Client connection error");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
        finally
        {
            // Остановка слушателя при выходе из цикла
            tcpListener.Stop();
        }
    }
    static void ServeClient(Object parameters)
    {
        TcpClient client = (TcpClient)parameters;
        Console.WriteLine($"Connected client {client.Client.LocalEndPoint}");
        var clientStream = client.GetStream();
        sslStream = new SslStream(clientStream, false);
        try
        {
            AutorizeServer(sslStream);
            string request = RecieveRequest(sslStream);
            Console.WriteLine($"USER {client.Client.RemoteEndPoint}: " + request);
            Console.WriteLine("-------------------------------------------------------------");

            if (request.StartsWith("GET"))
            {
                if (request.StartsWith("GET /styles.css"))
                {
                    ServeStaticFile(sslStream, "Page/styles.css", "text/css");
                }
                else if (request.StartsWith("GET /script.js"))
                {
                    ServeStaticFile(sslStream, "Page/script.js", "text/javascript");
                }
                else if (request.StartsWith("GET /result.html"))
                {
                    ServeStaticFile(sslStream, "Page/result.html", "text/html");
                }
                else
                {
                    SendHtmlPage(sslStream);
                }
            }
            else if (request.Contains("POST"))
            {
                HandlePostRequest(sslStream, request);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ServeClient Error: {ex.Message}");
        }
        finally
        {
            sslStream?.Close();
            client?.Close();
        }

    }
    static void ServeStaticFile(SslStream stream, string filePath, string contentType)
    {
        try
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);
            byte[] responseBytes = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: {fileBytes.Length}\r\n\r\n");
            stream.Write(responseBytes, 0, responseBytes.Length);
            stream.Write(fileBytes, 0, fileBytes.Length);
        }
        catch (FileNotFoundException)
        {
            // Обработка случая, если файл не найден
            byte[] responseBytes = Encoding.ASCII.GetBytes($"HTTP/1.1 404 Not Found\r\nContent-Type: text/plain\r\nContent-Length: 0\r\n\r\n");
            stream.Write(responseBytes, 0, responseBytes.Length);
        }
    }
    static void SendFileContent(SslStream stream, string filePath, string contentType)
    {
        byte[] fileBytes = File.ReadAllBytes(filePath);
        byte[] responseBytes = Encoding.ASCII.GetBytes($"HTTP/1.1 200 OK\r\nContent-Type: {contentType}\r\nContent-Length: {fileBytes.Length}\r\n\r\n");
        stream.Write(responseBytes, 0, responseBytes.Length);
        stream.Write(fileBytes, 0, fileBytes.Length);
    }
    static void SendHtmlPage(SslStream stream)
    {
        SendFileContent(stream, "Page/index.html", "text/html");
    }

    static void HandlePostRequest(SslStream stream, string request)
    {
        // Разделение строки запроса на параметры
        string[] parameters = request.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

        string input = parameters.FirstOrDefault(p => p.StartsWith("matrixA="));
        string[] matrixStrings = input.Split('&');
        string matrixALine = matrixStrings[0];
        string matrixBLine = matrixStrings[1];
        if (matrixALine != null && matrixBLine != null)
        {
            // Извлекаем значения матриц
            matrixALine = matrixALine.Substring("matrixA=".Length);
            matrixBLine = matrixBLine.Substring("matrixB=".Length);

            int[,] matrixA = ParseMatrix(matrixALine);
            int[,] matrixB = ParseMatrix(matrixBLine);

            // Выполнение умножения матриц
            int[,] result = MultiplyMatrices(matrixA, matrixB);

            // Сохранение результата в файл
            SaveMatrixToFile(result, "Page/result.html");

            // Отправка ответа клиенту
            SendFileContent(stream, "Page/result.html", "text/html");
        }
        else
        {
            // Ошибка в запросе - недостаточно данных
            byte[] responseBytes = Encoding.ASCII.GetBytes($"HTTP/1.1 400 Bad Request\r\nContent-Type: text/plain\r\nContent-Length: 0\r\n\r\n");
            stream.Write(responseBytes, 0, responseBytes.Length);
        }
    }
    static void SaveMatrixToFile(int[,] matrix, string filePath)
    {
        // Сохранение матрицы в файл
        using (StreamWriter writer = new StreamWriter(filePath))
        {
            writer.WriteLine("<html><head><title>Matrix Result</title>" +
                                "<link rel=\"stylesheet\" type=\"text/css\" href=\"styles.css\">" +
                                 "</head><body><h1>Matrix Result</h1><pre>");
            for (int i = 0; i < matrix.GetLength(0); i++)
            {
                for (int j = 0; j < matrix.GetLength(1); j++)
                {
                    writer.Write(matrix[i, j] + " ");
                }
                writer.WriteLine();
            }
            writer.WriteLine("</pre></body></html>");
        }
    }

    static int[,] ParseMatrix(string matrixLine)
    {
        // Декодирование строки, чтобы убрать двойную URL-кодировку
        matrixLine = WebUtility.UrlDecode(matrixLine);

        // Пример парсинга матрицы из строки с использованием новых строк в качестве разделителей
        string[] rowStrings = matrixLine.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        int rows = rowStrings.Length;

        if (rows == 0)
        {
            return new int[0, 0];
        }

        // Определяем количество столбцов, предполагая, что в первой строке матрицы будет хотя бы один элемент
        int cols = rowStrings[0].Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;

        int[,] matrix = new int[rows, cols];

        for (int i = 0; i < rows; i++)
        {
            string[] colStrings = rowStrings[i].Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int j = 0; j < cols; j++)
            {
                if (j < colStrings.Length)
                {
                    matrix[i, j] = int.Parse(colStrings[j]);
                }
                else
                {
                    // Добавляем проверку на случай, если в строке меньше элементов, чем ожидается
                    matrix[i, j] = 0;
                }
            }
        }

        return matrix;
    }

    static int[,] MultiplyMatrices(int[,] matrixA, int[,] matrixB)
    {
        int rowsA = matrixA.GetLength(0);
        int colsA = matrixA.GetLength(1);
        int rowsB = matrixB.GetLength(0);
        int colsB = matrixB.GetLength(1);

        if (colsA != rowsB)
        {
            throw new ArgumentException("Invalid matrix dimensions for multiplication");
        }

        int[,] result = new int[rowsA, colsB];

        for (int i = 0; i < rowsA; i++)
        {
            for (int j = 0; j < colsB; j++)
            {
                for (int k = 0; k < colsA; k++)
                {
                    result[i, j] += matrixA[i, k] * matrixB[k, j];
                }
            }
        }

        return result;
    }

    static string MatrixToString(int[,] matrix)
    {
        // Преобразование матрицы в строку для отправки в ответе
        StringBuilder resultString = new StringBuilder();
        resultString.Append("Result:");
        resultString.AppendLine();
        for (int i = 0; i < matrix.GetLength(0); i++)
        {
            for (int j = 0; j < matrix.GetLength(1); j++)
            {
                resultString.Append(matrix[i, j] + " ");
            }
            resultString.AppendLine(); // Новая строка для каждой строки матрицы
        }

        return resultString.ToString();
    }
    static string RecieveRequest(SslStream sslStream)
    {
        int byteSize = 16 * 1024;
        var buffer = new byte[byteSize];
        var messageData = new StringBuilder();
        int bytes;
        do
        {
            bytes = sslStream.Read(buffer, 0, buffer.Length);
            messageData.Append(Encoding.UTF8.GetString(buffer, 0, bytes));
        }
        while (bytes == byteSize);

        return messageData.ToString();
    }

    private static void AutorizeServer(SslStream clientStream)
    {
        try
        {
            clientStream.AuthenticateAsServer(certificate, clientCertificateRequired: false, SslProtocols.None, checkCertificateRevocation: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка аутентификации: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Внутреннее исключение: {ex.InnerException.Message}");
            }
            throw; // Перехватываем исключение, чтобы передать его дальше
        }
    }
}
