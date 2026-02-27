

There are some sourceTables that have a large amount of data (1 million to 50 million rows)

Retrieve all tables marked for migration [souceTables]
For each souceTable
 For each sourceRow in the souceTable
  Get targetRow
  If sourceRow is different from targetRow
   Update targetRow with sourceRow
  EndIf
 EndFor soureceRow
EndFor sourceTable

There are some sourceTables that have a large amount of data (1 million to 50 million rows)