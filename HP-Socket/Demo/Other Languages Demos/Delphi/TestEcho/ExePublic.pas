unit ExePublic;

interface

type
  PTMsg = ^TTMsg;
  TTMsg = record
    nType : Integer;
    //����ѡ��array of char������Ϊ����ָ������
    //����ֵ�ṩ�����ַ���ģʽ��
    nMsg : string[254];
  end;

implementation

end.
