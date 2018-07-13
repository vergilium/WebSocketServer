''' <summary>
'================================================
'  класс UserCollection
'================================================
''' </summary>
Public Class UserCollection
    Inherits Hashtable

    Public Event _Remove(ByVal Item As Object)
    'Private _isWebUser As Boolean = False

    'Public Property isWebUser()
    '    Get
    '        Return _isWebUser
    '    End Get
    '    Set(ByVal value)
    '        _isWebUser = value
    '    End Set
    'End Property
    ' добавление нового объекта с ключем gid в коллекцию
    Public Shadows Sub Add(ByVal gid As Object)
        Dim x As New User(gid)
        x.UserName = GenerateName()
        MyBase.Add(gid, x)
    End Sub
    ' очищаем коллекцию
    Public Shadows Sub Clear(ByVal gid As Object)
        RaiseEvent _Remove(MyBase.Item(gid))
    End Sub
    ' генерируем имя для пользователя
    Private Function GenerateName() As String
        Dim base As String = "Guest"
        For i As Integer = 1 To Me.Count
            If Not HasName(base & i) Then Return base & i
        Next
        Return base
    End Function
    ' функция поиска пользователя по его имени, возвращает wskGUID
    Public Function FindUsr(ByVal name As String, Optional ByVal case_sensitive As Boolean = False) As Object
        For Each usr As User In Me.Values
            If case_sensitive Then
                If usr.UserName = name Then Return usr.wskGUID
            Else
                If usr.UserName.ToLower = name.ToLower Then Return usr.wskGUID
            End If
        Next
        Return Nothing
    End Function
    ' проверка на существование пользователя с заданным именем (IP) в нашей коллекции
    Public Function HasName(ByVal name As String) As Boolean
        For Each usr As User In Me.Values
            If usr.UserName = name Then Return True
        Next
        Return False
    End Function

End Class

''' <summary>
'================================================
'  класс User для хранения информации о клиенте
'================================================
''' </summary>
Public Class User
    ' перечисление возможных типов клиентов
    Public Enum UserTypes
        UnknownClient = 0       ' неизвестный
        HookConService = 100    ' сервис "вытаскивания" данных колеса
        ClientPlayer = 1        ' клиент (игрок)
        WebServer = 500         ' веб-сервер
    End Enum

    ' конструктор
    Public Sub New(ByVal wsk As Object)
        _wsk = wsk
        _loggedIn = False
    End Sub

    ' объект сокета (внутренняя переменная класса)
    Private _socketObj As Connection
    ' объект сокета
    Public Property SocketObject() As Connection
        Get
            Return _socketObj
        End Get
        Set(ByVal value As Connection)
            _socketObj = value
        End Set
    End Property
    ' имя клиента (внутренняя переменная класса)
    Private _name As String
    ' имя клиента
    Public Property UserName() As String
        Get
            Return _name
        End Get
        Set(ByVal value As String)
            _name = value
        End Set
    End Property
    ' wskGUID, для идентификации (внутренняя переменная класса)
    Private _wsk As Object
    ' wskGUID, для идентификации
    Public ReadOnly Property wskGUID() As Object
        Get
            Return _wsk
        End Get
    End Property
    ' выполнен ли логин (внутренняя переменная класса)
    Private _loggedIn As Boolean
    ' выполнен ли логин
    Public Property IsLoggedIn() As Boolean
        Get
            Return _loggedIn
        End Get
        Set(ByVal value As Boolean)
            _loggedIn = value
        End Set
    End Property
    ' онлайн или оффлайн клиент (пока не реализовано)(внутренняя переменная класса)
    Private _online As Boolean
    ' онлайн или оффлайн клиент (пока не реализовано)
    Public Property IsOnline() As Boolean
        Get
            Return _online
        End Get
        Set(ByVal value As Boolean)
            _online = value
        End Set
    End Property
    ' IP клиента (внутренняя переменная класса):
    Private _IP As String
    ' имя терминала клиента (внутренняя переменная класса):
    Private _TRM As String
    ' порт клиента (внутренняя переменная класса):
    Private _port As Integer
    ' IP клиента:
    Public Property IP() As String
        Get
            Return _IP
        End Get
        Set(ByVal value As String)
            _IP = value
        End Set
    End Property
    ' имя терминала клиента:
    Public Property TRM As String
        Get
            TRM = _TRM
        End Get
        Set(ByVal value As String)
            _TRM = value
        End Set
    End Property
    ' порт клиента:
    Public Property Port() As Integer
        Get
            Return _port
        End Get
        Set(ByVal value As Integer)
            _port = value
        End Set
    End Property

    ' секция относится только к клиенту, являющимся игроком
#Region "Player Propertyes"
    ' ставки (внутренняя переменная класса):
    Private _bets As String
    ' ставки
    Public Property Bets() As String
        Get
            Return _bets
        End Get
        Set(ByVal value As String)
            _bets = value
        End Set
    End Property
    ' выигрыш (внутренняя переменная класса):
    Private _winValue As Integer
    ' выигрыш
    Public Property WinValue() As Integer
        Get
            Return _winValue
        End Get
        Set(ByVal value As Integer)
            _winValue = value
        End Set
    End Property
    ' кредит (внутренняя переменная класса):
    Private _credit As Integer
    ' кредит
    Public Property Credit() As Integer
        Get
            Return _credit
        End Get
        Set(ByVal value As Integer)
            _credit = value
        End Set
    End Property
#End Region
End Class