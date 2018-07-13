''' <summary>
'================================================
'  класс Connection для thread-safe конструкций
'  созданных подключений
'================================================
''' </summary>
Imports Alchemy.Classes                 ' импортируем библиотеку классов для веб-сокетов
Public Class Connection
    ' идентификатор
    Public ID As String
    ' имя терминала
    Public TRM As String
    ' время подключения клиента
    Public connectedTime As Date
    ' текущий статус (пока не реализовано)
    Public online As Boolean = False
    ' выполнен ли вход
    Public logon As Boolean = False
    ' на всякий пожарный (пока не используется)
    Public timer As System.Threading.Timer
    ' контекст вебСокет подключения пользователя
    Public Context As UserContext
    ' ===
    '  игровая шняга
    ' ===
    ' кредит клиента
    Public cr As Integer
    ' выигрыш клиента
    Public win As Integer
    ' ставки клиента
    Public _bets(0 To 37) As Double

    ' Инициализация класса, с передачей контекста подключения
    Public Sub New(ByVal ctx As UserContext)
        Context = ctx
    End Sub
    ' 
    Public Sub Connection()
        'Me.timer = New System.Threading.Timer(AddressOf TimerCallback, Nothing, 0, 1000)
    End Sub
    ' процедура обнуления массива ставок
    Public Sub NullBets()
        Array.Clear(_bets, 0, 37)
    End Sub
    ' процедура занесения ставок в массив
    Public Sub Bets(ByVal allBets() As String)
        For i = 0 To 36
            Dim dbl As Double
            If Double.TryParse(Replace(allBets(i), ".", ","), dbl) = True Then
                Dim rnddbl As Double = Math.Round(dbl, 3)
                _bets(i) = rnddbl
            Else
                Err.Raise(113, _bets, "Can't parse double value!")
            End If
        Next
    End Sub
    ' функция возвращающая сумму всех сделанных ставок
    Public Function GetSummBets() As Integer
        Dim ret As Double
        For i = 0 To 36
            ret += _bets(i)
        Next
        Return Math.Round(ret, 0)
    End Function
    ' процедура закрытия подключения
    Public Sub CloseConnection()
        Context.Send("close")
    End Sub
    'Private Sub TimerCallback(ByVal state As Object)
    '    Try
    '        '' Sending Data to the Client
    '        'Context.Send("[" + Context.ClientAddress.ToString() + "] " + System.DateTime.Now.ToString())
    '        'Context.Send("ping")
    '        'Threading.Thread.Sleep(5000)
    '        'online = False
    '    Catch ex As Exception
    '        Console.WriteLine(ex.Message)
    '    End Try

    'End Sub
End Class