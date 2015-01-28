
// ServerDlg.h : header file
//

#pragma once
#include "afxwin.h"

#include "../../../Src/HPSocket.h"
#include "../../Global/helper.h"

// CServerDlg dialog
class CServerDlg : public CDialogEx, public CUdpServerListener
{
// Construction
public:
	CServerDlg(CWnd* pParent = nullptr);	// standard constructor

// Dialog Data
	enum { IDD = IDD_SERVER_DIALOG };

protected:
	virtual void DoDataExchange(CDataExchange* pDX);	// DDX/DDV support
	virtual BOOL PreTranslateMessage(MSG* pMsg);

// Implementation
protected:
	HICON m_hIcon;

	// Generated message map functions
	virtual BOOL OnInitDialog();
	afx_msg void OnPaint();
	afx_msg HCURSOR OnQueryDragIcon();
	afx_msg void OnBnClickedStart();
	afx_msg void OnBnClickedStop();
	afx_msg void OnBnClickedStatistics();
	afx_msg LRESULT CServerDlg::OnUserInfoMsg(WPARAM wp, LPARAM lp);
	afx_msg int OnVKeyToItem(UINT nKey, CListBox* pListBox, UINT nIndex);
	afx_msg void OnClose();

	DECLARE_MESSAGE_MAP()
public:
	void SetAppState(EnAppState state);
	void Statistics();
	void Reset(BOOL bResetClientCount = TRUE);
private:
	virtual EnHandleResult OnPrepareListen(SOCKET soListen);
	virtual EnHandleResult OnSend(CONNID dwConnID, const BYTE* pData, int iLength);
	virtual EnHandleResult OnReceive(CONNID dwConnID, const BYTE* pData, int iLength);
	virtual EnHandleResult OnClose(CONNID dwConnID);
	virtual EnHandleResult OnError(CONNID dwConnID, EnSocketOperation enOperation, int iErrorCode);
	virtual EnHandleResult OnAccept(CONNID dwConnID, const SOCKADDR_IN* pSockAddr);
	virtual EnHandleResult OnShutdown();

private:
	CListBox m_Info;
	CEdit m_Port;
	CComboBox m_SendPolicy;
	CComboBox m_RecvPolicy;
	CComboBox m_RecvCount;
	CButton m_Start;
	CButton m_Stop;
	CButton m_Statistics;
	EnAppState m_enState;

	volatile LONGLONG m_llTotalReceived;
	volatile LONGLONG m_llTotalSent;
	volatile LONG m_lClientCount;

	CCriSec m_cs;
	CUdpServerPtr m_Server;
};
