

''' <summary>
''' enum describes the type for history read operation.
''' </summary>
Public Enum HistoryReadOperation
    ''' <summary>
    ''' Subscribe to data changes.
    ''' </summary>
    Subscribe = 1

    ''' <summary>
    ''' Read raw data.
    ''' </summary>
    Raw = 2

    ''' <summary>
    ''' Read modified data.
    ''' </summary>
    Modified = 3

    ''' <summary>
    ''' Read data at the specified times.
    ''' </summary>
    AtTime = 4

    ''' <summary>
    ''' Read processed data.
    ''' </summary>
    Processed = 5

    ''' <summary>
    ''' Insert data.
    ''' </summary>
    Insert = 6

    ''' <summary>
    ''' Insert or replace data.
    ''' </summary>
    InsertReplace = 7

    ''' <summary>
    ''' Replace data.
    ''' </summary>
    Replace = 8

    ''' <summary>
    ''' Remove data.
    ''' </summary>
    Remove = 9

    ''' <summary>
    ''' Delete raw data.
    ''' </summary>
    DeleteRaw = 10

    ''' <summary>
    ''' Delete modified data.
    ''' </summary>
    DeleteModified = 11

    ''' <summary>
    ''' Delete data at the specified times.
    ''' </summary>
    DeleteAtTime = 12


    ''' <summary>
    ''' exit command
    ''' </summary>
    [exit] = 13
End Enum
