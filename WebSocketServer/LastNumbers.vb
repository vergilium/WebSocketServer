Public Class LastNumbers
    Dim _allNums As New ArrayList

    Public Sub Add(ByVal newN As Integer)
        If _allNums.ToArray.Length >= 13 Then
            _allNums.RemoveAt(0)
            _allNums.Add(newN)
        Else
            _allNums.Add(newN)
        End If
    End Sub
    Public Function GetStr(Optional ByVal separator As String = ",") As String
        Dim retStr As String = ""
        For i = 0 To _allNums.Count - 1
            retStr = _allNums(i).ToString() & "," & retStr
        Next
        Return retStr
    End Function
End Class
