''' <summary>
'********************************************************
' RRoulette WebSocket Server
' Консольная программа-сервер 
'********************************************************
' Осуществляет подключения посредством WebSockets к 
' браузерам клиентов и дальнейшее прём/передачу данных от
' клиента главному серверу.
'********************************************************
'                                                05/16/13
' Copyright (c) seriy-coder
' for Capitan
' Mysterion Live 2013 ☺
' [seriy-coder@ya.ru]
'********************************************************
''' </summary>
Imports Alchemy.Classes                 ' импортируем библиотеку классов для веб-сокетов
Imports System.Threading.Thread         ' импортируем библиотеку потоков
Imports System.Collections.Concurrent   ' импортируем библиотеку потоко-безопасных коллекций
Imports Winsock_Orcas                   ' импортируем библиотеку WinSock

Module Program
    ' необходимые переменные и перечисления
#Region "Globals"
    Friend OnlineConnections = New ConcurrentDictionary(Of String, Object)()            ' thread-safe список подключенных клиентов
    Dim logLevel As Integer = 2                                                         ' уровень логирования консоли
    Dim debugPhpFile As String                                                          ' путь к файлу php отладки клиента
    Dim debugMode As DebugModes                                                         ' уровень отладки
    Dim debugList As New Collection                                                     ' список клиентов
    Private WithEvents client As New Winsock_Orcas.Winsock                              ' винсок-объект для подключения к основному серверу
    'Dim timer As System.Threading.Timer                                                ' таймер ?
    Dim pingtmrint As Long, donePing As Boolean                                         ' нужные для проверки ping'а переменные
    Dim gamestate As Boolean                                                            ' состояние игры (ставки принимаются/ставки приняты)
    Dim fixstate As Boolean = False                                                     ' флаг фиксации состояния игры (не даёт меняться состоянию игры)
    Dim WithEvents _users As New UserCollection                                         ' коллекция клиентов

    ' перечисление типов режимов отладки клиентов
    Enum DebugModes
        debug_none = 0      ' без отладки
        debug_list = 1      ' отладка только для конкретных IP содержащихся в debugList
        debug_all = 2       ' отладочный уровень на всех без исключени клиентах
    End Enum
    ' перечисление типов выводимых надписей
    Enum DisplayStyle
        Information = ConsoleColor.Yellow       ' информационная строка
        Log = ConsoleColor.White                ' логирование
        Text = ConsoleColor.DarkGreen           ' простой текст
        Alert = ConsoleColor.Red                ' ошибка/предупреждения
        Title = ConsoleColor.Gray               ' заголовок
        DebugInfo = ConsoleColor.Blue           ' просто отображение информации в консоль, без записи в файл
        LogFile = -233884                       ' просто запись в файл, без отображения в консоли
    End Enum
#End Region
    ' вспомогательные процедуры веб-сервера
#Region "Routines"

    ' функция проверяет является ли строка s валидным IP адресом
    Public Function IsIP(ByVal s As String) As Boolean
        Dim dots As Byte
        Dim dbldots As Byte
        Dim others As Byte

        For i = 1 To Len(s)
            Dim chr As String
            chr = Mid(s, i, 1) ' получаем очередной символ строки
            If (chr = ".") Then
                dots = dots + 1
            ElseIf (chr = ":") Then
                dbldots = dbldots + 1
            Else
                others = others + 1
            End If
        Next i
        If dots = 3 And dbldots = 1 Then Return True Else Return False
    End Function

    ' вывод в консоль
    Sub wrLine(Optional ByVal text As String = "", Optional ByVal style As DisplayStyle = DisplayStyle.Information)
        If text = "" Then Console.WriteLine() : Exit Sub

        If style = DisplayStyle.LogFile Then
            Try
                IO.File.AppendAllText(CurDir() & "\wss_" & Date.Now.ToString("dd_MM_yy") & ".log", Format(Date.Now, "dd.MM.yy HH:mm:ss") + " :: " + text + vbNewLine)
            Catch ex As Exception
            End Try

            Exit Sub
        End If

        Dim oldc As ConsoleColor = Console.ForegroundColor, oldbc As ConsoleColor = Console.BackgroundColor

        Console.ForegroundColor = style
        If style = DisplayStyle.Title Then
            Console.BackgroundColor = ConsoleColor.DarkBlue
        ElseIf style = DisplayStyle.Log Then
            'Console.BackgroundColor = ConsoleColor.DarkGray
        End If

        If text.IndexOf(vbNewLine) >= 0 Then
            Dim ln() As String = text.Split(vbNewLine)
            Dim l As Long
            For l = LBound(ln) To UBound(ln)
                Try
                    Console.WriteLine(IIf(style = DisplayStyle.Log, Format(Date.Now, "HH:mm:ss") + " >> ", "") + ln(l).Substring(1, ln(l).Length - 2))
                    If style = DisplayStyle.Log Or style = DisplayStyle.Alert Then
                        Try
                            IO.File.AppendAllText(CurDir() & "\wss_" & Date.Now.ToString("dd_MM_yy") & ".log", Format(Date.Now, "dd.MM.yy HH:mm:ss") + " :: " + text + vbNewLine)
                        Catch ex As Exception
                        End Try
                    End If
                Catch ex As Exception
                End Try
            Next
        Else
            Console.WriteLine(IIf(style = DisplayStyle.Log, Format(Date.Now, "HH:mm:ss") + " >> ", "") + text)
            If style = DisplayStyle.Log Or style = DisplayStyle.Alert Then
                Try
                    IO.File.AppendAllText(CurDir() & "\wss_" & Date.Now.ToString("dd_MM_yy") & ".log", Format(Date.Now, "dd.MM.yy HH:mm:ss") + " :: " + text + vbNewLine)
                Catch ex As Exception
                End Try
            End If
        End If
        Console.ForegroundColor = oldc
        Console.BackgroundColor = oldbc
    End Sub

    ' изменяем режим уровня отладки, путём создания в папке веб-сервера
    ' файла debug.php с необходимыми функциями
    ' именно тут он и создаётся. и создаётся только исключительно из веб-сервера
    Sub ChangeDmode()
        Dim ff As String() = System.IO.File.ReadAllLines(debugPhpFile)
        Dim nf As String(), c As Integer = 0
        Dim inFunc As Boolean = False

        For i = 0 To ff.Length - 1
            If ff(i) = "// DLVL -->" Then
                inFunc = True

                Dim lst As String = ""
                For ii = 1 To debugList.Count
                    lst = lst + Chr(34) + debugList.Item(i) + Chr(34) + ", "
                Next
                If lst.Length > 0 Then lst = lst.Substring(0, lst.Length - 2)

                Dim newF As String = "// DLVL -->" + vbNewLine + _
                "function getDmodeLevel() {" + vbNewLine + _
                " return " + CInt(debugMode).ToString + ";" + vbNewLine + _
                "};" + vbNewLine + _
                "// <-- DLVL"

                ReDim Preserve nf(c)
                nf(c) = newF
                c += 1
            ElseIf ff(i) = "// <-- DLVL" Then
                inFunc = False
            Else
                If Not inFunc Then
                    ReDim Preserve nf(c)
                    nf(c) = ff(i)
                    c += 1
                End If
            End If
        Next
        System.IO.File.WriteAllLines(debugPhpFile, nf)
    End Sub

    ' а в данной процедуре создаётся список клиентов, для 
    ' уровня отладки 1, когда отладка включена только для
    ' определенных IP
    Sub ChangeDmodeList()
        Dim ff As String() = System.IO.File.ReadAllLines(debugPhpFile)
        Dim nf As String(), c As Integer = 0
        Dim inFunc As Boolean = False

        For i = 0 To ff.Length - 1
            If ff(i) = "// DLST -->" Then
                inFunc = True

                Dim lst As String = ""
                For ii = 1 To debugList.Count
                    lst = lst + Chr(34) + debugList.Item(ii) + Chr(34) + ", "
                Next
                If lst.Length > 0 Then lst = lst.Substring(0, lst.Length - 2)

                Dim newF As String = "// DLST -->" + vbNewLine + _
                "function getDmodeList() {" + vbNewLine + _
                " $list=array(" + lst + ");" + vbNewLine + _
                " return $list;" + vbNewLine + _
                "};" + vbNewLine + _
                "// <-- DLST"

                ReDim Preserve nf(c)
                nf(c) = newF
                c += 1
            ElseIf ff(i) = "// <-- DLST" Then
                inFunc = False
            Else
                If Not inFunc Then
                    ReDim Preserve nf(c)
                    nf(c) = ff(i)
                    c += 1
                End If
            End If
        Next
        System.IO.File.WriteAllLines(debugPhpFile, nf)
    End Sub

    ' процедура создания файла отладки
    ' по-умолчанию
    ' по-умолчанию режим отладки 0 (вообще без отладки)
    Sub BuildDefDmode()
        Dim txt As String = "<?php " + vbNewLine + _
            "// debug.php - debug functions file" + vbNewLine +
            "// DO NOT MODIFY!! THIS FILE AUTOMATICALLY CREATED BY WebSocketServer FOR DEBUG ROUTINES" + vbNewLine +
            "// DLVL -->" + vbNewLine +
            "function getDmodeLevel() { " + vbNewLine +
            " return 0; " + vbNewLine +
            "}; " + vbNewLine +
            "// <-- DLVL" + vbNewLine +
            "// DLST -->" + vbNewLine +
            "function getDmodeList() { " + vbNewLine +
            " $list=array();" + vbNewLine +
            " return $list; " + vbNewLine +
            "};" + vbNewLine +
            "// <-- DLST" + vbNewLine +
            "if (isset($_GET['get_dmode'])){" + vbNewLine +
            " echo(getDmodeLevel());" + vbNewLine +
            "};" + vbNewLine +
            "?>"
        System.IO.File.WriteAllText(debugPhpFile, txt)
    End Sub

    'Private Sub TimerCallback(ByVal state As Object)
    '    Dim myNonPersisterMemoryMappedFile As MemoryMappedFile = MemoryMappedFile.OpenExisting("WebSocketServer")
    '    Dim mymutex As Mutex = Mutex.OpenExisting("WebSocketServerMutex")
    '    mymutex.WaitOne()
    '    Dim sr As StreamReader = New StreamReader(myNonPersisterMemoryMappedFile.CreateViewStream)
    '    Dim ln As String = sr.ReadLine()
    '    sr.Close()
    '    mymutex.ReleaseMutex()

    '    If ln <> oldLn And ln <> "" Then
    '        phpHelperEvent(ln)
    '        mymutex.WaitOne()
    '        Dim sw As StreamWriter = New StreamWriter(myNonPersisterMemoryMappedFile.CreateViewStream)
    '        sw.WriteLine("")
    '        mymutex.ReleaseMutex()
    '        oldLn = ln
    '    End If
    'End Sub
#End Region

    ' процедуры подключения/отключения к основному серверу
#Region "Server"
    ' процедура подключения к основному серверу
    Sub ConnectToServer()
        If logLevel > 0 Then wrLine("Try to connect Remote Roulet Server", DisplayStyle.Title)
        debugPhpFile = My.Settings.debugPhpFile
        client.LegacySupport = True
        client.RemoteHost = My.Settings.Server
        client.RemotePort = My.Settings.Port
        client.Connect()
    End Sub

    ' процедура отключения от основного сервера
    Sub DisconnectFromServer()
        client.Close()
    End Sub
#End Region
    ' основная часть веб-сервера
#Region "Main Programm"

    ' Наша функция Main, с которой начинается выполнение программы
    ' все происходит тут. гоняем бесконечно по циклу и считываем команды, которые можно вводить
    ' в консоль.
    Sub Main()
        Dim aServer = New Alchemy.WebSocketServer(16669, System.Net.IPAddress.Any)      ' начинаем слушать порт на входящие вебСокет соединения

        With aServer
            .OnConnected = AddressOf OnConnect          ' указываем коллбек ф-цию для события OnConnected
            .OnReceive = AddressOf OnReceive            ' указываем коллбек ф-цию для события OnReceive
            .OnSend = AddressOf OnSend                  ' указываем коллбек ф-цию для события OnSend
            .OnDisconnect = AddressOf OnDisconnect      ' указываем коллбек ф-цию для события OnDisconnect
            .TimeOut = New TimeSpan(24, 0, 0, 0)
        End With

        aServer.Start()     ' запускаем сервер

        Console.ForegroundColor = ConsoleColor.Yellow
        Console.Title = "RRoulette WebSocket Server"
        Console.WriteLine("Running Alchemy WebSocket Server ...")
        Console.WriteLine("[Type 'exit' to stop the server, 'help' to list of all commands]", DisplayStyle.title)
        ConnectToServer()                               ' подключаемся к основному серверу
        BuildDefDmode()                                 ' создаём дефолтный файл настроек отладки
        Dim command As String = String.Empty, cmd As String, param() As String, tmp() As String

        While (command <> "exit" Or command <> "quit")      ' пока не ввели команду для выхода, бегаем по циклу
            command = Console.ReadLine()

            tmp = Split(command, " ", 2)                ' получаем основную команду
            cmd = tmp(0)

            Select Case cmd
                Case "help"
                    ' отображение помощи
                    wrLine("*** *** *** All server commands: *** *** ***", DisplayStyle.Title)
                    wrLine("[list] - Show list of connected(online) users.")
                    wrLine("[send <ip>, <data>] - Send <data> to <ip> client.")
                    wrLine(" <data> list of commands:")
                    wrLine("    number # - send Win number")
                    wrLine("    endisp, disdisp - enable/disable game area")
                    wrLine("    log on/off - enable/disable logging from client")
                    wrLine("    refresh - refresh client window")

                    wrLine("[send_all <data>] - Send <data> to all connected clients.")
                    wrLine("[send_all_but1 <but_ip> <data>] - Send <data> to all connected clients,")
                    wrLine("    but client with ip <but_ip>.")
                    wrLine("[log_level <int>] - Sets the log level of the server. Must be 0, 1 or 2.")
                    wrLine("     0 - No log, but errors; 1 - Medium log level (show important)")
                    wrLine("     messages) [default]; 2 - High log level (show all messages, with dump's and others).")
                    wrLine("[restart] - Restart the server")
                    wrLine("[dmode] - return/sets debugMode for clients (0,1 or 2)")
                    wrLine("    0 - debugMode off; 1 - debugMode on only for debugClients list;")
                    wrLine("    2 - debugMode is global (for all clients)")
                    wrLine("[dmode_list <command>] - Work with debugClient list. Without parameters")
                    wrLine("    return current list.")
                    wrLine(" <command> list of commands:")
                    wrLine("    add ip1[;ipN] -- adds specified IP's to list")
                    wrLine("    remove ip1[;ipN] -- removes specified IP's from list")
                    wrLine("    clear -- clears the list")
                    wrLine("[credit] <ip> [<value>] - Return/sets the current credit of client.")
                    wrLine("[game_state] <state> - Return/sets the game state (0 or 1)")
                    wrLine("[fix_game_state] <state> - Return/sets the fixed game state (0 or 1)")
                    wrLine("[win] <number> - Raise win event with <number>")
                    wrLine("[connect_rr] - Connect to RR Server")
                    wrLine("[ping <ip>] - ping client")
                    wrLine("[clear] - Clear the console")
                    wrLine("[exit] - Stop the server")
                    wrLine("*********************************************")
                    wrLine()

                Case "dmode"
                    ' выставляем/смотрим уровень отладки
                    If UBound(tmp) > 0 Then
                        debugMode = CInt(tmp(1))
                        ChangeDmode()
                    Else
                        wrLine("Current debugMode is: " + debugMode.ToString)
                    End If

                Case "win"
                    ' указываем выигрышный номер
                    If UBound(tmp) = 1 Then
                        Dim subComm() As String = Split(tmp(1), " ", 1)
                        SendMsg2All("number " & subComm(0)) ' отсылаем всем выигрышный номер
                        CalculateWin(subComm(0))
                    Else
                        wrLine("Usage: win <number>")
                    End If

                Case "credit"
                    ' устанавливаем/смотрим кредит у клиента
                    If UBound(tmp) > 0 Then
                        Dim subComm() As String = Split(tmp(1), " ", 2)
                        If UBound(subComm) = 0 Then
                            wrLine("Credit value of client " & subComm(0) & " is: " & GetConnObject(subComm(0)).cr.ToString())
                        Else
                            GetConnObject(subComm(0)).cr = CInt(subComm(1))
                            SendMsg("cr " & subComm(1), subComm(0))
                        End If
                    Else
                        wrLine("USAGE: credit <ip> [<value>] // ip - IPaddr of client, value - credit summ", DisplayStyle.Information)
                    End If
                Case "dmode_list"
                    ' управление списком отлаживаемых клиентов
                    Dim errFlag As Boolean = False

                    If UBound(tmp) > 0 Then
                        Dim subComm() As String = Split(tmp(1), " ", 2)
                        Select Case subComm(0)
                            Case "add"
                                ' добавить адрес
                                If UBound(subComm) = 1 Then
                                    Dim addr() As String = Split(subComm(1), ";")
                                    For i = 0 To UBound(addr)
                                        debugList.Add(addr(i), addr(i))
                                    Next
                                    ChangeDmodeList()
                                Else
                                    errFlag = True
                                End If
                            Case "remove"
                                ' удалить адрес
                                If UBound(subComm) = 1 Then
                                    Dim addr() As String = Split(subComm(1), ";")
                                    For i = 0 To UBound(addr)
                                        debugList.Remove(addr(i))
                                    Next
                                    ChangeDmodeList()
                                Else
                                    errFlag = True
                                End If
                            Case "clear"
                                ' очистить список
                                debugList.Clear()
                                ChangeDmodeList()
                            Case Else
                                errFlag = True
                        End Select
                    Else
                        Dim ret As String = ""
                        For i = 1 To debugList.Count
                            ret = ret + debugList.Item(i) + ";"
                        Next
                        If ret.Length > 0 Then
                            ret = ret.Substring(0, ret.Length - 1)
                            wrLine("debugList: " + ret)
                        Else
                            wrLine("debugList is empty!")
                        End If
                    End If
                    If errFlag Then wrLine("Not enought parameters. Type 'help' to view right syntax.", DisplayStyle.Alert)

                Case "ping"
                    ' пингуем клиента
                    If UBound(tmp) > 0 Then
                        pingtmrint = Date.Now.Millisecond
                        doneping = False
                        Dim datap As New String(Chr(Int(Rnd() * 255)), 35)
                        SendMsg("ping " & datap.ToString, tmp(1))
                        'pingThread = New System.Threading.Timer(AddressOf thPingWait, Nothing, 0, 20000)
                    Else
                        wrLine("Please, specify IP to ping", DisplayStyle.Alert)
                    End If

                Case "connect_rr"
                    ' подключаемся к основному серверу
                    If client.State <> Winsock_Orcas.WinsockStates.Connected Then
                        client.Connect()
                    End If

                Case "log_level"
                    ' установить/считать уровень логирования в консоль
                    If UBound(tmp) = 0 Then
                        wrLine("Current log level is: " & logLevel)
                    Else
                        Try
                            Dim l As Integer = CType(tmp(1), Integer)
                            If l < 0 Or l > 3 Then
                                wrLine("Invalid log level value. Value must be 0, 1 or 2", DisplayStyle.Alert)
                            Else
                                logLevel = l
                                wrLine("Current log level set to: " & logLevel)
                            End If
                        Catch ex As Exception
                            If Err.Number = 13 Then
                                wrLine("Log level value must be an integer!", DisplayStyle.Alert)
                            Else
                                wrLine(ex.ToString, DisplayStyle.Alert)
                            End If

                        End Try
                    End If

                Case "restart"
                    ' перезагрузка веб-сервера
                    aServer.Stop()
                    aServer.Start()
                    DisconnectFromServer()
                    ConnectToServer()
                    Console.Clear()
                    wrLine("Server restarted success")

                Case "list"
                    ' получить список клиентов
                    GetListClients()

                Case "send_all"
                    ' отослать данные всем клиентам
                    SendMsg2All(tmp(1))

                Case "send_all_but1"
                    ' отослать данные всем клиентам кроме одного
                    param = Split(tmp(1), " ", 2)
                    SendMsg2AllBut1(param(1), param(0))

                Case "send"
                    ' отослать данные конкретному клиенту
                    If UBound(tmp) > 0 Then
                        param = Split(tmp(1), " ", 2)
                        If UBound(param) >= 1 Then
                            SendMsg(param(1), param(0))
                        Else
                            wrLine("Not enought parameters", DisplayStyle.Alert)
                        End If
                    Else
                        wrLine("Not enought parameters", DisplayStyle.Alert)
                    End If

                Case "clear"
                    ' очистить консоль
                    Console.Clear()

                Case "exit", "quit"
                    ' закрыть сервер
                    wrLine("Server was exit. Good be!", DisplayStyle.Information)
                    Exit While
                Case "game_state"
                    If UBound(tmp) = 0 Then
                        wrLine("Current gamestate is : " + gamestate.ToString)
                    Else
                        Try
                            gamestate = CBool(tmp(1))
                        Catch ex As Exception

                        End Try
                    End If
                    SendMsg2All(IIf(gamestate, "endisp", "disdisp"))

                Case "fix_game_state"
                    If UBound(tmp) = 0 Then
                        wrLine("Current fixgamestate is : " + fixstate.ToString)
                    Else
                        Try
                            fixstate = CBool(tmp(1))
                        Catch ex As Exception

                        End Try
                    End If
                Case Else
                    ' борода!!!!
                    wrLine("Unrecognized command '" & cmd & "'. Type 'help' to list of all avaiable commands.", DisplayStyle.Alert)
            End Select
        End While
        aServer.Stop()
        Sleep(5000)
        End
    End Sub
#End Region

    ' реализация процедур комманд консоли сервера
#Region "Server Commands"

    ' получение списка подключенных клиентов
    Sub GetListClients()
        Dim txt As String
        wrLine("Count of online users: " & OnlineConnections.count)
        For Each Connection In OnlineConnections
            txt = txt & "-- Client IP: " & Connection.key.ToString.Split(":")(0) & " | Use protocol: " & Connection.value.context.protocol.ToString & " | Use port:" & Connection.key.ToString.Split(":")(1) & " " & vbNewLine & _
                " Online: " & _
                DateDiff(DateInterval.Day, Connection.value.connectedTime, Date.Now) & "d." & _
                DateDiff(DateInterval.Hour, Connection.value.connectedTime, Date.Now) - DateDiff(DateInterval.Day, Connection.value.connectedTime, Date.Now) * 24 & "h." & _
                DateDiff(DateInterval.Minute, Connection.value.connectedTime, Date.Now) - DateDiff(DateInterval.Hour, Connection.value.connectedTime, Date.Now) * 60 & "m." & _
                DateDiff(DateInterval.Second, Connection.value.connectedTime, Date.Now) - DateDiff(DateInterval.Minute, Connection.value.connectedTime, Date.Now) * 60 & "sec. | " & _
                "Current credit:" & Connection.value.cr.ToString & "  " & vbNewLine

            '" - connection time: " & Format(Connection.value.connectedTime, "dd.MM.yy HH:mm:ss") & _
        Next
        wrLine("-------------------------------------" & vbNewLine &
               txt & _
               "-------------------------------------" & vbNewLine)
    End Sub

    ' отправка сообщения основному серверу
    Sub SendSrvMsg(ByVal text As String)
        If client.State = WinsockStates.Connected Then
            client.Send(text)
        End If
    End Sub

    ' отправка сообщения конкретному клиенту
    Sub SendMsg(ByVal text As String, ByVal Name As String)
        If InStr(Name, ":") > 0 Then Name = Name.Split(":")(0)
        If logLevel = 1 Then wrLine("[" & Name & "] Send data : [" & text.Length.ToString & " bytess]", DisplayStyle.Log)
        If logLevel = 2 Then wrLine("[" & Name & "] Send data [" & text & "]", DisplayStyle.Log)
        For Each Connection In OnlineConnections
            If Connection.key.ToString.Split(":")(0) = Name Then
                Connection.value.context.send(text)
                Exit Sub
            End If
        Next
        wrLine("Client " & Name & " not found!", DisplayStyle.Alert)
    End Sub

    ' отправка сообщения всем клиентам
    Sub SendMsg2All(ByVal text As String)
        If logLevel = 1 Then wrLine("Send data to all clients" & " [" & text.Length.ToString & " bytess]", DisplayStyle.Log)
        If logLevel = 2 Then wrLine("Send data [" & text & "] to all clients", DisplayStyle.Log)
        For Each Connection In OnlineConnections
            Connection.value.context.send(text)
        Next
    End Sub

    ' отправка сообщения всем клиентам, кроме указанного
    Sub SendMsg2AllBut1(ByVal text As String, ByVal butName As String)
        If logLevel = 1 Then wrLine("Send data to all clients, but client: " & butName & " [" & text.Length.ToString & " bytess]", DisplayStyle.Log)
        If logLevel = 2 Then wrLine("Send data [" & text & "] to all clients, but client: " & butName, DisplayStyle.Log)
        For Each Connection In OnlineConnections
            If Connection.key.ToString.Split(":")(0) <> butName Then
                Connection.value.context.send(text)
            End If
        Next
    End Sub

    'проверка существования клиента с заданным IP
    Function CheckIP(ByVal ip As String) As Boolean
        If InStr(ip, ":") Then ip = ip.Split(":")(0)
        For Each Connection In OnlineConnections
            If Connection.key.ToString.Split(":")(0) = ip Then Return True
        Next
        Return False
    End Function

    'получение времени подключения клиента
    Function GetDateConn(ByVal ip As String) As Date
        For Each Connection In OnlineConnections
            If Connection.key.ToString.Split(":")(0) = ip Then
                Return Connection.value.context.connectedTime
            End If
        Next
    End Function

    ' получаем IP по имени терминала
    Function GetIPbyTRM(ByVal TRM As String) As String
        For Each Connection In OnlineConnections
            If Connection.value.TRM = TRM Then
                Return Connection.key.ToString
            End If
        Next
        Return False
    End Function

    ' получаем объект Connection по айпи или имени терминала
    Function GetConnObject(ByVal Addr As String, Optional ByVal IsTermName As Boolean = False) As Connection
        If IsTermName = False Then
            If InStr(Addr, ":") > 0 Then Addr = Addr.Split(":")(0)
            For Each Connection In OnlineConnections
                If Connection.key.ToString.Split(":")(0) = Addr Then
                    Return Connection.value
                End If
            Next
            Return Nothing
        Else
            For Each Connection In OnlineConnections
                If Connection.value.TRM = Addr Then
                    Return Connection.value
                End If
            Next
            Return Nothing
        End If
    End Function
#End Region

    ' вся логика работы веб-сервера
#Region "Server Logic"

    ' процедура обработки событий пришедших по веб-сокету из админки
    Public Sub phpHelperEvent(ByVal cmd As String, Optional ByVal ctx As UserContext = Nothing)
        If logLevel > 0 Then wrLine("Try to execute command [" & cmd & "]")
        Dim parseCmd() As String = cmd.Split(" ")
        Dim parseSubCmd() As String = Nothing
        If UBound(parseCmd) > 0 Then
            parseSubCmd = parseCmd(1).Split(";")
        End If

        Select Case parseCmd(0)
            Case "add_credit"
                If ((parseSubCmd Is Nothing) <> True) And UBound(parseSubCmd) = 2 Then
                    If CInt(parseSubCmd(1)) <= 0 Then
                        Try
                            GetConnObject(parseSubCmd(0), True).cr = 0
                        Catch ex As Exception
                        End Try
                        client.Send("/SQL TAKE " & parseCmd(1))
                        'SendMsg("cr " & GetIPbyTRM(parseSubCmd(1)), parseSubCmd(0))
                        wrLine("Take credit from Administrator:" & parseSubCmd(2))
                    Else
                        Try
                            GetConnObject(parseSubCmd(0), True).cr = CInt(parseSubCmd(1))
                        Catch ex As Exception
                        End Try
                        client.Send("/SQL ADD " & parseCmd(1))
                        'SendMsg("cr " & GetIPbyTRM(parseSubCmd(1)), parseSubCmd(0))
                        wrLine("Add credit [" & parseSubCmd(1) & "] from Administrator:" & parseSubCmd(2))
                    End If
                End If
                Dim conn As New Connection(ctx)
                OnlineConnections.TryRemove(ctx.ClientAddress.ToString(), conn)
                'Case "login"
                '    client.Send("/GET_CR " & GetConnObject(parseSubCmd(0)).TRM)
        End Select
    End Sub

    ' расщет выигрышей у клиентов
    Sub CalculateWin(ByVal n As String)
        For Each Connection In OnlineConnections
            Dim nn As Integer = CInt(n)

            Connection.value.win = Math.Round(Connection.value._bets(nn) * 36)
            wrLine("[before] => .win=" & Connection.value.win & " .cr=" & Connection.value.cr & " GetSummBets()=" & Connection.value.GetSummBets(), DisplayStyle.DebugInfo)
            Connection.value.cr = (Connection.value.cr - Connection.value.GetSummBets()) + Connection.value.win
            wrLine("[after] => .win=" & Connection.value.win & " .cr=" & Connection.value.cr, DisplayStyle.DebugInfo)
            'Connection.value.cr += Connection.value.win
            'SendMsg("cr " + Connection.value.cr.ToString(), Connection.value.ID)
            SendSrvMsg("/SQL CHANGE_CR " + Connection.value.TRM + ";" + Connection.value.cr.ToString())
            Connection.value.context.send("win " & Connection.value.win.ToString)
            'Connection.value.
            'Dim i As Integer = Math.Round(Connection.value._bets(nn) * 36)
            Connection.value.NullBets()
        Next
    End Sub
#End Region

    ' коллбек процедуры веб-сокета
#Region "Server Callbacks"

    ' событие подключения клиента
    Sub OnConnect(ByVal aContext As UserContext)
        wrLine("[" & aContext.ClientAddress.ToString() & "] Client Connected.")
        '' создаём новое подключение через сласс Connection
        Dim conn As Connection = New Connection(aContext)
        '' проверяем, было ли уже подключение с данного адреса и
        '' добавляем оъект в thread-safe коллекцию
        Dim usr As User = _users.Item(aContext.ClientAddress)

        If usr Is Nothing Then
            conn.connectedTime = Date.Now
            usr = New User(aContext.ClientAddress)
            usr.UserName = aContext.ClientAddress.ToString.Split(":")(0)
            usr.SocketObject = conn

            _users.Add(aContext.ClientAddress)
            _users(aContext.ClientAddress) = usr
        ElseIf usr.IsLoggedIn = True Then
            conn = _users(aContext.ClientAddress).SocketObject
        ElseIf usr.IsLoggedIn = False Then
            conn.connectedTime = Date.Now
            _users(aContext.ClientAddress).SocketObject = conn
        End If
        _users(aContext.ClientAddress).IsOnline = True
        OnlineConnections.TryAdd(aContext.ClientAddress.ToString(), conn)
        conn.ID = aContext.ClientAddress.ToString
        'aContext.Send("init " & allNums.GetStr())
    End Sub

    ' событие получения данных от клиента
    Sub OnReceive(ByVal aContext As UserContext)
        'Try
        If logLevel = 2 Then wrLine("[" & aContext.ClientAddress.ToString() & "]" & " Data Received => " + aContext.DataFrame.ToString(), DisplayStyle.Log)

        Dim cmd As String, sArr() As String, param() As String      '  набор переменных для анализа пришедших данных
        sArr = Split(aContext.DataFrame.ToString, " ", 2)           '  блоки данных делятся пробелом. получаем основную команду
        cmd = sArr(0)
        If UBound(sArr) > 0 Then
            param = Split(sArr(1), " ")                             ' если имеем дополнительные данные, так-же режем их по пробелу
        End If

        If logLevel = 1 Then
            Dim str_p As String = ""
            If IsNothing(param) Then
                str_p = " with no arguments."
            Else
                If param(0).Length = 0 Then
                    str_p = " with no arguments."
                Else
                    str_p = " with arguments (" + String.Join(", ", param) + ")."
                End If
            End If
            wrLine("Client " + aContext.ClientAddress.ToString() + " send command '" + cmd + "'" + str_p, DisplayStyle.Log)
        End If
        ' анализ основной команды
        Select Case cmd
            Case "logoff"
                _users(aContext.ClientAddress).IsLoggedIn = False
            Case "init"
                ' запрос данных инициализации
                GetConnObject(aContext.ClientAddress.ToString).logon = True
                GetConnObject(aContext.ClientAddress.ToString).TRM = param(0)
                If ServerClosed = False Then
                    SendMsg(IIf(gamestate, "init endisp;" & allNums.GetStr(), "init disdisp;" & allNums.GetStr()), aContext.ClientAddress.ToString)        ' отсылаем текущее состояние игры
                Else
                    SendMsg("server_down", aContext.ClientAddress.ToString)
                End If

                SendSrvMsg("/WCCON " & aContext.ClientAddress.ToString.Split(":")(0) & " " & GetConnObject(aContext.ClientAddress.ToString).TRM) ' сообщаем серверу, что подключился веб-клиент
                SendSrvMsg("/GET_CR " & param(0))                                                       ' требуем от сервера текущее состояние кредита терминала

                'SendMsg("cr " + GetConnObject(aContext.ClientAddress.ToString).cr.ToString, aContext.ClientAddress.ToString)
            Case "bets"
                ' клиент сообщает о сделанных ставках
                If sArr(1).Length = 0 Then Return
                Dim allb() As String = Split(sArr(1), ";")
                Dim rets As String

                'Dim betsArr() As Long, bc As Long = 0
                Dim summ As Double

                rets = "User was do next bets:" + vbNewLine
                For i = 0 To 36
                    rets = rets & "#" & i & ":"
                    Dim dbl As Double
                    If Double.TryParse(Replace(allb(i), ".", ","), dbl) = True Then
                        Dim rnddbl As Double = Math.Round(dbl, 3)
                        rets += rnddbl.ToString & " " & vbNewLine
                        summ += rnddbl
                    Else
                        wrLine("Error parsing double string. Maybe used unsupported separator. Must be '.'" & vbNewLine & _
                               "Source : " & allb(i).ToString, DisplayStyle.Alert)
                    End If
                Next

                If summ > 0 Then
                    wrLine(rets)
                    GetConnObject(aContext.ClientAddress.ToString()).Bets(allb)
                End If

            Case "pong"
                ' ответ пинга
                wrLine("Pong recived. Ping time : " & (Date.Now.Millisecond - pingtmrint) & " ms.")
                doneping = True

            Case "_callback"
                ' команда пришедшая из админки
                phpHelperEvent(sArr(1), aContext)

        End Select
    End Sub

    ' событие отправки данных клиенту
    Sub OnSend(ByVal aContext As UserContext)
        Try
            If logLevel = 1 Then wrLine("[" & aContext.ClientAddress.ToString() & "] [" & aContext.DataFrame.Length.ToString & " bytess] Sent", DisplayStyle.Log)
            If logLevel = 2 Then wrLine("[" & aContext.ClientAddress.ToString() & "] [" & aContext.DataFrame.ToString & "] Sent", DisplayStyle.Log)
        Catch ex As Exception
        wrLine("An error occured in OnSend() event." & vbNewLine & ex.ToString(), DisplayStyle.Alert)
        End Try
    End Sub

    ' событие разрыва связи с клиентом
    Sub OnDisconnect(ByVal aContext As UserContext)
        If logLevel > 0 Then wrLine("Client Disconnected : " + aContext.ClientAddress.ToString(), DisplayStyle.Log)
        '' Remove the connection Object from the thread-safe collection
        Dim conn As Connection = New Connection(aContext)
        'If GetConnObject(aContext.ClientAddress.ToString).logon = False Then
        If _users IsNot Nothing And _users.Count > 0 Then
            If _users(aContext.ClientAddress).IsLoggedIn = True Then
                SendSrvMsg("/WCDISCON " & aContext.ClientAddress.ToString.Split(":")(0) & " OFFLINE")
                _users(aContext.ClientAddress).IsOnline = False
            Else
                SendSrvMsg("/WCDISCON " & aContext.ClientAddress.ToString.Split(":")(0) & " LOGOFF")
                _users(aContext.ClientAddress).IsOnline = False
            End If
            OnlineConnections.TryRemove(aContext.ClientAddress.ToString(), conn)
        End If
        'End If
    End Sub
#End Region

    ' логика работы с основным сервером
#Region "Remote Server"
    Dim ServerClosed As Boolean = True      ' флаг состояния подключения основного сервера
    Dim allNums As New LastNumbers          ' последние 13 выпавших номеров

    ' событие подключения основного сервера
    Private Sub client_Connected(ByVal sender As Object, ByVal e As Winsock_Orcas.WinsockConnectedEventArgs) Handles client.Connected
        If logLevel > 0 Then wrLine("Done connecting to RR Server!")
        ' отправляем начальные данные инициализации клиенту
        Try
            client.Send("/INITWS " & client.LocalIP(2) & " " & client.LocalPort)
        Catch ex As Exception
            Try
                client.Send("/INITWS " & client.LocalIP(1) & " " & client.LocalPort)
            Catch ex1 As Exception
                client.Send("/INITWS " & client.LocalIP(0) & " " & client.LocalPort)
            End Try
        End Try

        If _users.Count > 0 Then
            For Each Connection In OnlineConnections
                Connection.value.context.send("refresh")
                client.Send("/WCCON " & Connection.value.context.clientaddress.ToString.Split(":")(0) & " " & GetConnObject(Connection.value.context.clientaddress.ToString).TRM)
            Next
        End If
        ServerClosed = False
    End Sub

    ' событие получения данных от основного сервера
    Private Sub client_DataArrival(ByVal sender As Object, ByVal e As Winsock_Orcas.WinsockDataArrivalEventArgs) Handles client.DataArrival
        Dim obj As Object = sender.Get() ' получаем объект от которого пришли данные
        Dim s As String = CStr(System.Text.Encoding.UTF8.GetString(obj)) ' получаем строку
        Dim dp As New DataPacket(s) ' преобразуем к DataPacket'у, для удобства работы
        Dim args() As String

        ' анализ пришедшей команды
        Select Case dp.Command
            Case "GWIN"
                ' шарик выпал
                args = dp.Arguments(2)
                SendMsg2All("number " & args(1)) ' отсылаем всем выигрышный номер
                CalculateWin(args(1))
                allNums.Add(args(1))

            Case "WSTOP"
                ' начало принятия ставок
                SendMsg2All("endisp")
                gamestate = True

            Case "WRUN"
                ' конец принятия ставок
                SendMsg2All("disdisp")
                gamestate = False

            Case "SET_CR"
                ' сообщаем клиенту его текущий кредит
                args = dp.Arguments(2)
                Dim IP As String = GetIPbyTRM(args(0))
                If IsIP(IP) <> False Then
                    Try
                        GetConnObject(IP).cr = CInt(args(1))
                        SendMsg("cr " & args(1), IP)
                    Catch ex As Exception
                        wrLine("Can't add credit to " & IP & vbNewLine & ex.ToString(), DisplayStyle.Alert)
                    End Try
                End If

            Case "DISCONNECT"
                ' отключился основной сервер
                args = dp.Arguments(1)
                wrLine("Disconnecting from RR Server! Reason: " & args(0), DisplayStyle.Alert)
                SendMsg2All("server_down")
                ServerClosed = True
                client.Close()

            Case "DISCONCLI"
                ' отключился веб-клиент
                args = dp.Arguments(2)
                wrLine("[" & args(0) & "] Disconnect from server. Reason: " & args(1))
                GetConnObject(args(0)).CloseConnection()

            Case "INITDATA"
                ' получение последних 13ти выпавших номеров
                args = dp.Arguments(1)
                Dim ic As ICollection = args(0).Split(",")
                For i = 0 To UBound(ic) - 1
                    allNums.Add(CInt(ic(i)))
                Next
                wrLine("LastNumbers: " & allNums.GetStr(), DisplayStyle.Log)

        End Select
    End Sub

    ' событие отключения основного сервера
    Private Sub client_Disconnected(ByVal sender As Object, ByVal e As System.EventArgs) Handles client.Disconnected
        If Not ServerClosed Then
            wrLine("Disconnecting from RR Server!", DisplayStyle.Alert)
            SendMsg2All("server_down")
        End If
        ServerClosed = True
    End Sub

    ' ошибка основного сервера...
    Private Sub client_ErrorReceived(ByVal sender As Object, ByVal e As Winsock_Orcas.WinsockErrorReceivedEventArgs) Handles client.ErrorReceived
        wrLine(e.Message, DisplayStyle.Alert)
    End Sub
#End Region
End Module
