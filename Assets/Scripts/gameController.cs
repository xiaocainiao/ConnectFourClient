using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System;



public class gameController : MonoBehaviour {
    

	private int boardRows = 6;
	private int boardCols = 7;
	private int[,] board;
    private int[,] newBoard;

    private int curPlayerID = 2;
    private GameObject currentDisc;
    private bool isfalling;

    private float offsetX = 0.82f;
    private float offsetY = 0.88f;

	[SerializeField]
	private GameObject DiscBlack;
	[SerializeField]
	private GameObject DiscWhite;

    private bool isGameOver;

    private string _ip = "127.0.0.1";
    private int _port = 8080;
    private TcpClient sockconn;
    private Thread clientReceiveThread;



    //判断是否平局
    public bool isTied(){
        for (int i = 0; i < boardRows; i++){
            for (int j = 0; j < boardCols; j++){
                if(board[i, j] == 0)
                    return false;
            }
        }
        return true;
    }

    //判断是否胜利
    public bool IsVictory(int row, int col){
        //水平方向
        if (GetAdj(row, col, 0, -1) + GetAdj(row, col, 0, 1) > 2){
            return true;
        }else{
            //垂直方向
            if(GetAdj(row, col, -1, 0) > 2){
                return true;
            }else{
                //左下角和右上角
                if(GetAdj(row, col, -1, -1) + GetAdj(row, col, 1, 1) > 2){
                    return true;
                }else{
                    //右下角和左上角
                    if (GetAdj(row, col, -1, 1) + GetAdj(row, col, 1, -1) > 2)
                    {
                        return true;
                    }else{
                        return false;
                    }
                }
            }
        }
    }

    private int BoardValue(int row, int col){
        if(row >= 0 && row <= boardRows-1 && col >= 0 && col <= boardCols-1){
            return board[row, col];
        }
        return -1;
    }

    //递归检测周边与自己相同的棋子的数目
    private int GetAdj(int row, int col, int row_inc, int col_inc){
        if(BoardValue(row, col) == BoardValue(row+row_inc, col+col_inc) && BoardValue(row,col)!=-1 && BoardValue(row+row_inc, col+col_inc)!=-1
           && BoardValue(row, col)!=0 && BoardValue(row+row_inc, col+col_inc)!=0){
            return 1 + GetAdj(row + row_inc, col + col_inc, row_inc, col_inc);
        }else{
            return 0;
        }
    }

    public List<int> GetPossibleCol(){
        List<int> possibleCol = new List<int>();
        for (int i = 0; i < boardCols; i++){
            if(board[boardRows-1, i] == 0){
                possibleCol.Add(i);
            }
        }
        return possibleCol;
    }

    //找到指定列上第一个空行的索引
    private int FirstEmptyRow(int colIndex, int playerID){
        int rowIndex = -1;
        for (int i = boardRows-1; i >= 0; i--){
            if(board[i, colIndex] != 0){
                rowIndex = i;
                break;
            }
        }

        if(rowIndex+1 != boardRows){
            board[rowIndex + 1, colIndex] = playerID;
            return rowIndex + 1;
        }
        return -1;
    }

    //投掷棋子,鼠标左键按下触发
    private IEnumerator DropDisc(GameObject disc){
        isfalling = true;
        int colIndex = Mathf.RoundToInt((disc.transform.position.x)/offsetX);
        int rowIndex = FirstEmptyRow(colIndex, curPlayerID);
        if(rowIndex == -1){
            isfalling = false;
            yield break;
        }
            
        Vector3 startPos = new Vector3(colIndex * offsetX, disc.transform.position.y, disc.transform.position.z);
        Vector3 endPos = new Vector3(colIndex*offsetX, rowIndex*offsetY, disc.transform.position.z);
        GameObject newDisc = Instantiate(disc) as GameObject;
        disc.GetComponent<SpriteRenderer>().enabled = false;

        float timer = 0;
        while(timer < 1){
            timer += Time.deltaTime;
            newDisc.transform.position = Vector3.Lerp(startPos, endPos, timer);
            yield return null;
        }

        //立即销毁原来的棋子
        DestroyImmediate(disc);
        isGameOver = (isTied() || IsVictory(rowIndex, colIndex));
        if (!isGameOver){
            currentDisc = AddDisc(3 - curPlayerID);
        }
        isfalling = false;
        yield return null;
    }

    //棋子随鼠标水平移动
    private void MoveHorizontal(){
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        if(currentDisc){
            currentDisc.transform.position = new Vector3(Mathf.Clamp(mousePos.x, 0f, (boardCols-1)*offsetX), (boardRows + 1) * offsetY, 0.5f);
        }
    }

    // 暂停执行几秒中
    private IEnumerator DelayTime(float sec){ 
        print ("wait"); 
        yield return new WaitForSeconds(sec);
    }   

    private GameObject AddDisc(int playerID){
        curPlayerID = playerID;
        //屏幕坐标转化为世界坐标
        Vector3 spawnPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        if(curPlayerID == 1){
            //List<int> move = GetPossibleCol();

            if (newBoard == null)
            {
                Debug.Log("还没有结果");
                StartCoroutine(DelayTime(15f)); 
            }

            int AIcol = FindAgentMove();
            Debug.Log("AI column : "+AIcol.ToString());
            if (AIcol == -1)
            {
                Debug.Log("还没有结果");
            }
            else
            {
                
                spawnPos = new Vector3(AIcol * offsetX, (boardRows + 1) * offsetY, 0.5f);
            }

        }

        GameObject disc = Instantiate((playerID == 1)?DiscBlack:DiscWhite, new Vector3(spawnPos.x, (boardCols + 1) * offsetY, 0.5f), Quaternion.identity) as GameObject;
        return disc;
    }

	private void InitialBoard(){
		board = new int[boardRows, boardCols];
		for(int i = 0; i < boardRows; i++){
			for(int j = 0; j < boardCols; j++){
				board[i,j] = 0;
			}
		}
	}

    private string PrintBoard(){
        StringBuilder sb = new StringBuilder();
		for(int i = boardRows - 1; i >= 0; i--){
			for(int j = 0; j < boardCols; j++){
				//sb.Append(board[i,j].ToString()+" ");
                if (j == boardCols - 1)
                {
                    sb.Append(board[i, j].ToString());
                }
                else
                {
                    sb.Append(board[i, j].ToString() + " ");
                }
			}
			sb.Append("\n");
		}
		Debug.Log(sb.ToString());
        return sb.ToString();
	}

    // 将获取的字符串转为二维数组
    private void strToBoard(string boardStr){
        newBoard = new int[boardRows, boardCols];
        for (int i = 0; i < boardRows; i++){
            for (int j = 0; j < boardCols; j++){
                newBoard[i, j] = 0;
            }
        }

        if (string.IsNullOrEmpty(boardStr))
            return ;

        string[] strArray = boardStr.Trim().Split('\n');
        for (int i = 0; i < strArray.Length; i++){
            string[] eachChar = strArray[i].Trim().Split(' ');
            for (int j = 0; j < eachChar.Length; j++){
                newBoard[boardRows-i-1, j] = Convert.ToInt32(eachChar[j]);
            }
        }
    }

    //AIagent返回的move
    private int FindAgentMove()
    {
        for (int i = 0; i < boardRows; i++)
        {
            for (int j = 0; j < boardCols; j++)
            {
                if (board[i, j] != newBoard[i, j])
                {
                    return j;
                }
            }
        }
        return -1;
    }

    //client通信
    //连接服务器
    private void ConnectServer()
    {
        try
        {
            clientReceiveThread = new Thread(new ThreadStart(ListenForData));
            clientReceiveThread.IsBackground = true;
            clientReceiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.Log("On client connect exception " + e);
        }
    }

    //接收数据
    private void ListenForData()
    {
        try
        {
            sockconn = new TcpClient(_ip, _port);
            Byte[] bytes = new Byte[512];
            while (true)
            {
                // Get a stream object for reading              
                using (NetworkStream stream = sockconn.GetStream())
                {
                    int length;
                    // Read incomming stream into byte arrary.                  
                    while ((length = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        var incommingData = new byte[length];
                        Array.Copy(bytes, 0, incommingData, 0, length);
                        // Convert byte array to string message.                        
                        string serverMessage = Encoding.ASCII.GetString(incommingData);
                        strToBoard(serverMessage);
                        Debug.Log("server message received as: " + serverMessage);
                    }
                }
            }
        }
        catch (SocketException socketException)
        {
            Debug.Log("Socket exception: " + socketException);
        }

    }

    //发送数据
    private void SendMessage(string chessStr)
    {
        if (sockconn == null)
            return;
        try
        {
            NetworkStream stream = sockconn.GetStream();
            if (stream.CanWrite)
            {
                //string clientMessage = "Client from Unity.";
                string clientMessage = chessStr;
                byte[] clientMessageAsByteArray = Encoding.ASCII.GetBytes(clientMessage);
                stream.Write(clientMessageAsByteArray, 0, clientMessageAsByteArray.Length);
                Debug.Log("Client sent his message - should be received by server");
            }
        }
        catch (SocketException se)
        {
            Debug.Log("Socket exception: " + se);
        }
    }

	// Use this for initialization
	void Start () {
        ConnectServer();
		InitialBoard();
        currentDisc = AddDisc(2);
	}
	
	// Update is called once per frame
	void Update () {
        if(curPlayerID == 2){
            if (Input.GetMouseButtonDown(0) && !isfalling)
            {
                if (currentDisc)
                {
                    StartCoroutine(DropDisc(currentDisc));
                }
                SendMessage(PrintBoard());
            }

            MoveHorizontal();
        }else{
            if(!isfalling){
                StartCoroutine(DropDisc(currentDisc));
            }
        }
       
		if(Input.GetKeyUp(KeyCode.P)){
			PrintBoard();
           
		}
        if(Input.GetKeyUp(KeyCode.Return)){
            Debug.Log("重新载入游戏");
            UnityEngine.SceneManagement.SceneManager.LoadScene(0);
        }
        if(Input.GetKeyUp(KeyCode.Space) || Input.GetKeyUp(KeyCode.Escape)){
            Debug.Log("退出游戏");
            Application.Quit();
        }

	}

    private void OnGUI()
    {
        GUIStyle fontStyle = new GUIStyle();
        fontStyle.alignment = TextAnchor.MiddleCenter;
        fontStyle.fontSize = 50;
        fontStyle.normal.textColor = Color.white;
        fontStyle.normal.background = null;

        if (isGameOver)
        {
            string res = (curPlayerID == 1) ? "电脑" : "玩家";
            Debug.Log("游戏结束!" + res + "获胜!");

            if (curPlayerID == 1)
            {
                GUI.Button(new Rect(Screen.width * 0.3f, Screen.height * 0.4f, Screen.width * 0.4f, Screen.height * 0.25f), "电脑获胜！");
            }
            else
            {
                GUI.Button(new Rect(Screen.width * 0.3f, Screen.height * 0.4f, Screen.width * 0.4f, Screen.height * 0.25f), "玩家获胜！");
            }
        }
    }
}
