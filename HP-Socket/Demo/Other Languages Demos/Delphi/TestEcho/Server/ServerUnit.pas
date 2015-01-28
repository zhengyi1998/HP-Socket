unit ServerUnit;

interface

uses
    Windows, Messages, SysUtils, Variants, Classes, Graphics,
    Controls, Forms, Dialogs, StdCtrls, HPSocketSDKUnit, ExePublic;

type

    TForm1 = class(TForm)
        lstMsg: TListBox;
        edtIpAddress: TEdit;
        edtConnId: TEdit;
        lbl1: TLabel;
        btnDisConn: TButton;
        btnStart: TButton;
        btnStop: TButton;
        procedure FormCreate(Sender: TObject);
        procedure btnStartClick(Sender: TObject);
        procedure btnStopClick(Sender: TObject);
        procedure btnDisConnClick(Sender: TObject);
        procedure edtConnIdChange(Sender: TObject);
        procedure lstMsgKeyPress(Sender: TObject; var Key: Char);
        procedure FormClose(Sender: TObject; var Action: TCloseAction);
    private
        { Private declarations }
        procedure AddMsg(msg: string);
        procedure SetAppState(state: EnAppState);
    public
        { Public declarations }
        procedure OnRec(dwConnId: DWORD; const pData: Pointer; iLength: Integer);
    end;

    //������ܿͻ��˽ṹ��
    TDoSerRec = class(TThread)
      FId : DWORD;
      RecMsg : PTMsg;
      procedure showmsg;
    protected
      procedure Execute;override;
    public
      //�ش����ͻ���
      ilen : Integer;
      pdata : Pointer;

      constructor Create(connid : DWORD);
    end;

var
    Form1: TForm1;
    appState: EnAppState;
    pServer: Pointer;
    pListener: Pointer;

implementation

{$R *.dfm}

procedure TForm1.SetAppState(state: EnAppState);
begin
    appState := state;
    btnStart.Enabled := (appState = EnAppState.ST_STOPED);
    btnStop.Enabled := (appState = EnAppState.ST_STARTED);
    edtIpAddress.Enabled := (appState = EnAppState.ST_STOPED);
    edtConnId.Enabled := (appState = EnAppState.ST_STARTED);
    btnDisConn.Enabled := ((appState = EnAppState.ST_STARTED) and (Length(edtConnId.Text) > 0));
end;

procedure TForm1.AddMsg(msg: string);
begin
    if lstMsg.Items.Count > 100 then
    begin
        lstMsg.Items.Clear;
    end;
    lstMsg.Items.Add(msg);
end;

function SendString(dwConnId: DWORD; str: string): Boolean;
var
    sendBuffer: array of byte;
    sendStr: AnsiString;
    sendLength: Integer;
begin
    sendStr := AnsiString(str);
    // ��ȡansi�ַ����ĳ���
    sendLength := Length(sendStr);
    // ����buf����ĳ���
    SetLength(sendBuffer, sendLength);
    // �������ݵ�buf����
    Move(sendStr[1], sendBuffer[1], sendLength);

    Result := HP_Server_Send(pServer, dwConnId, sendBuffer, sendLength);
end;

function OnPrepareListen(soListen: Pointer): En_HP_HandleResult; stdcall;
begin

    Result := HP_HR_OK;
end;

function OnAccept(dwConnId: DWORD; pClient: Pointer): En_HP_HandleResult; stdcall;
var
    ip: array [0 .. 40] of WideChar;
    ipLength: Integer;
    port: USHORT;
begin
    ipLength := 40;
    if HP_Server_GetRemoteAddress(pServer, dwConnId, ip, @ipLength, @port) then
    begin
        Form1.AddMsg(Format(' > [%d,OnAccept] -> PASS(%s:%d)', [dwConnId, string(ip), port]));
    end
    else
    begin
        Form1.AddMsg(Format(' > [[%d,OnAccept] -> HP_Server_GetClientAddress() Error', [dwConnId]));
    end;

    Result := HP_HR_OK;
end;

function OnServerShutdown(): En_HP_HandleResult; stdcall;
begin

    Form1.AddMsg(' > [OnServerShutdown]');
    Result := HP_HR_OK;
end;

function OnSend(dwConnId: DWORD; const pData: Pointer; iLength: Integer): En_HP_HandleResult; stdcall;
begin
    Form1.AddMsg(Format(' > [%d,OnSend] -> (%d bytes)', [dwConnId, iLength]));
    Result := HP_HR_OK;
end;

function OnReceive(dwConnId: DWORD; const pData: Pointer; iLength: Integer): En_HP_HandleResult; stdcall;
var
    testString: AnsiString;

    doRec : TDoSerRec;
begin
    Form1.AddMsg(Format(' > [%d,OnReceive] -> (%d bytes)', [dwConnId, iLength]));

    {// ������һ��pDataת�ַ�������ʾ
    SetLength(testString, iLength);
    Move(pData^, testString[1],  iLength);
    Form1.AddMsg(Format(' > [%d,OnReceive] -> say:%s', [dwConnId, testString]));


    if HP_Server_Send(pServer, dwConnId, pData, iLength) then
    begin
        Result := HP_HR_OK;
    end
    else
    begin
        Result := HP_HR_ERROR;
    end;
    }

    Form1.OnRec(dwConnId, pData, iLength);

    Result := HP_HR_OK;
end;

function OnCloseConn(dwConnId: DWORD): En_HP_HandleResult; stdcall;
begin

    Form1.AddMsg(Format(' > [%d,OnCloseConn]', [dwConnId]));
    Result := HP_HR_OK;
end;

function OnError(dwConnId: DWORD; enOperation: En_HP_SocketOperation; iErrorCode: Integer): En_HP_HandleResult; stdcall;
begin

    Form1.AddMsg(Format('> [%d,OnError] -> OP:%d,CODE:%d', [dwConnId, Integer(enOperation), iErrorCode]));
    Result := HP_HR_OK;
end;

procedure TForm1.btnDisConnClick(Sender: TObject);
var
    dwConnId: DWORD;
begin
    dwConnId := StrToInt(edtConnId.Text);
    if HP_Server_Disconnect(pServer, dwConnId, 1) then
        Form1.AddMsg(Format('$(%d) Disconnect OK', [dwConnId]))
    else
        Form1.AddMsg(Format('$(%d) Disconnect Error', [dwConnId]));

end;

procedure TForm1.btnStartClick(Sender: TObject);
var
    ip: PWideChar;
    port: USHORT;
    errorId: En_HP_SocketError;
    errorMsg: PWideChar;
begin
    ip := PWideChar(edtIpAddress.Text);
    port := 5555;
    SetAppState(ST_STARTING);
    if HP_Server_Start(pServer, ip, port) then
    begin
        AddMsg(Format('$Server Start OK -> (%s:%d)', [ip, port]));
        SetAppState(ST_STARTED);
    end
    else
    begin
        errorId := HP_Server_GetLastError(pServer);
        errorMsg := HP_Server_GetLastErrorDesc(pServer);
        AddMsg(Format('$Server Start Error -> %s(%d)', [errorMsg, Integer(errorId)]));
        SetAppState(ST_STOPED);
    end;
end;

procedure TForm1.btnStopClick(Sender: TObject);
var
    errorId: En_HP_SocketError;
    errorMsg: PWideChar;
begin

    SetAppState(ST_STOPING);
    AddMsg('$Server Stop');
    if HP_Server_Stop(pServer) then
    begin
        SetAppState(ST_STOPED);
    end
    else
    begin
        errorId := HP_Server_GetLastError(pServer);
        errorMsg := HP_Server_GetLastErrorDesc(pServer);
        AddMsg(Format('$Stop Error -> %s(%d)', [errorMsg, Integer(errorId)]));

    end;
end;

procedure TForm1.edtConnIdChange(Sender: TObject);
begin
    btnDisConn.Enabled := ((appState = ST_STARTED) and (Length(edtConnId.Text) > 0));
end;

procedure TForm1.FormClose(Sender: TObject; var Action: TCloseAction);
begin
    // ���� Socket ����
    Destroy_HP_TcpServer(pServer);
    // ���ټ���������
    Destroy_HP_TcpServerListener(pListener);
end;

procedure TForm1.FormCreate(Sender: TObject);
begin
    // ��������������
    pListener := Create_HP_TcpServerListener();

    // ���� Socket ����
    pServer := Create_HP_TcpServer(pListener);

    // ���� Socket �������ص�����
    HP_Set_FN_Server_OnPrepareListen(pListener, OnPrepareListen);
    HP_Set_FN_Server_OnAccept(pListener, OnAccept);
    HP_Set_FN_Server_OnSend(pListener, OnSend);
    HP_Set_FN_Server_OnReceive(pListener, OnReceive);
    HP_Set_FN_Server_OnClose(pListener, OnCloseConn);
    HP_Set_FN_Server_OnError(pListener, OnError);
    HP_Set_FN_Server_OnShutdown(pListener, OnServerShutdown);

    SetAppState(ST_STOPED);
end;

procedure TForm1.lstMsgKeyPress(Sender: TObject; var Key: Char);
begin
    if (Key = 'c') or (Key = 'C') then
        lstMsg.Items.Clear;

end;

procedure TForm1.OnRec(dwConnId: DWORD; const pData: Pointer;
  iLength: Integer);
var
  doRec : TDoSerRec;

  SendMsg, RecMsg : PTMsg;
begin
  doRec := TDoSerRec.Create(dwConnId);
  //������ָ��
  Move(pdata, dorec.pdata, iLength);
  doRec.ilen := iLength;
  doRec.Resume;
end;

{ TDoSerRec }

constructor TDoSerRec.Create(connid: DWORD);
begin
  inherited Create(True);
  FId := connid;
end;

procedure TDoSerRec.Execute;
var
  SendMsg : PTMsg;
begin
  inherited;
  try
    Move(pdata, RecMsg, ilen);
    try
      case RecMsg.nType of
        1000 : begin
                 //������Ϣ
                 Synchronize(showmsg);
                 New(SendMsg);
                 try
                   SendMsg.nType := 1001;
                   SendMsg.nMsg := 'Do Rec; Ready do other thing?';
                   HP_Server_Send(pServer, FId, SendMsg, SizeOf(ttmsg));
                 finally
                   Dispose(SendMsg);
                 end;
               end;
      end;
    except

    end;
  finally

  end;
end;

procedure TDoSerRec.showmsg;
begin
  form1.AddMsg('Rec from client:' + RecMsg.nMsg);
end;

end.
