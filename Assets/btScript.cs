using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Newtonsoft.Json;
using KMHelper;

public class btScript : MonoBehaviour
{
    #region vars

    public KMAudio Audio;
    public KMBombModule Module;
    public KMBombInfo Info;
    public KMSelectable[] NodeBtns;
    public TextMesh[] NodeBtnText;
    public TextMesh[] ScreenDisplays;

    private static int _moduleIdCounter = 1;
    private int _moduleId = 0;
    private bool _isSolved = false, _lightsOn = false, _isStrike = false;
    private float strikeMarkTime = 0f;

    private static readonly int[] preOrder = { 0, 1, 3, 4, 2, 5, 6 };
    private static readonly int[] inOrder = { 3, 1, 4, 0, 5, 2, 6 };
    private static readonly int[] postOrder = { 3, 4, 1, 5, 6, 2, 0 };
    private static readonly int[] preOrderF = { 0, 2, 6, 5, 1, 4, 3 };
    private static readonly int[] inOrderF = { 6, 2, 5, 0, 4, 1, 3 };
    private static readonly int[] postOrderF = { 6, 5, 2, 4, 3, 1, 0 };
    private static readonly int[] lvlOrderF = { 0, 2, 1, 6, 5, 4, 3 };

    private static readonly int[] orderStage2Mappings = { 5, 6, 4, 3, 0, 1, 7, 2 };
    private static readonly int[] orderStage3Mappings = { 7, 1, 5, 0, 3, 6, 2, 4 };

    private static readonly Color[] orderColors = { Color.red, Color.green, new Color(0f, 0.297f, 0.797f), new Color(1f, 0.5f, 0f), Color.cyan, Color.magenta, Color.yellow, new Color(0.3f, 0.3f, 0.3f) };
    private static readonly string[] orderColorNames = { "red", "green", "blue", "orange", "cyan", "magenta", "yellow", "gray" };
    private static readonly char[] orderColorChars = { 'R', 'G', 'B', 'O', 'C', 'M', 'Y', 'A' };

    private int[] btnOrders = new int[7];
    private bool[] btnReverse = new bool[7];
    private char[] btnChars = new char[7];
    private int prevCorrectInd = -1;
    private int stage;

    #endregion

    #region initialization

    //loading
    void Start()
    {
        _moduleId = _moduleIdCounter++;
        initPuzzle();
        initDisplays();
        _lightsOn = true;
        setTextColors(false);
        Module.GetComponent<KMGameInfo>().OnLightsChange += OnLightChange;
        Debug.LogFormat("[Binary Tree #{0}] Tree Structure:\n" +
            "(Key: (Button Color, Char, Text Color), colors are Red, Green, Blue, Magenta, Cyan, Yellow, Orange, grAy, Silver, blacK.)\n" +
            "              ({1}, {2}, {3})\n" +
            "              /       \\\n" +
            "             /         \\\n" +
            "            /           \\\n" +
            "           /             \\\n" +
            "      ({4}, {5}, {6})       ({7}, {8}, {9})\n" +
            "      /    \\             /    \\\n" +
            "     /      \\           /      \\\n" +
            "({10}, {11}, {12})({13}, {14}, {15}) ({16}, {17}, {18})({19}, {20}, {21})", _moduleId,
            orderColorChars[btnOrders[0]], btnChars[0], btnReverse[0] ? 'S' : 'K',
            orderColorChars[btnOrders[1]], btnChars[1], btnReverse[1] ? 'S' : 'K',
            orderColorChars[btnOrders[2]], btnChars[2], btnReverse[2] ? 'S' : 'K',
            orderColorChars[btnOrders[3]], btnChars[3], btnReverse[3] ? 'S' : 'K',
            orderColorChars[btnOrders[4]], btnChars[4], btnReverse[4] ? 'S' : 'K',
            orderColorChars[btnOrders[5]], btnChars[5], btnReverse[5] ? 'S' : 'K',
            orderColorChars[btnOrders[6]], btnChars[6], btnReverse[6] ? 'S' : 'K');

        for (int i = 0; i < 7; i++)
        {
            int j = i;
            NodeBtns[i].OnInteract += delegate ()
            {
                nodeBtnPress(j);
                return false;
            };
        }
    }

    void OnLightChange(bool isOn)
    {
        setTextColors(!isOn);
    }

    void setTextColors(bool hidden)
    {
        if (hidden)
        {
            for (int i = 0; i < 7; i++)
                NodeBtnText[i].color = Color.black;
        }
        else
        {
            for (int i = 0; i < 7; i++)
                NodeBtnText[i].color = btnReverse[i] ? new Color(0.6f, 0.6f, 0.6f) : Color.black;
        }
    }

    void initPuzzle()
    {
        for (int i = 0; i < 7; i++)
        {
            btnOrders[i] = Random.Range(0, 8);
            NodeBtns[i].GetComponent<MeshRenderer>().material.color = orderColors[btnOrders[i]];
            btnReverse[i] = Random.Range(0, 2) != 0 ? true : false;
            NodeBtnText[i].color = btnReverse[i] ? new Color(0.6f, 0.6f, 0.6f) : Color.black;
            btnChars[i] = numToChar(Random.Range(0, 36));
            NodeBtnText[i].text = btnChars[i].ToString();
        }
        stage = 1;
    }

    void initDisplays()
    {
        for (int i = 0; i < 4; i++)
            ScreenDisplays[i].text = "";
    }

    #endregion

    #region Button Handling & Answer Calculation

    void nodeBtnPress(int num)
    {
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, NodeBtns[num].transform);
        NodeBtns[num].AddInteractionPunch();
        if (_lightsOn && !_isSolved && !_isStrike)
        {
            Debug.LogFormat("[Binary Tree #{0}] Pressed button: {1} {2}! Checking answer...", _moduleId, orderColorNames[btnOrders[num]], btnChars[num]);
            int refBtnInd, op;
            if (stage == 1)
            {
                refBtnInd = 0;
                op = btnOrders[refBtnInd];
            }
            else
            {
                refBtnInd = prevCorrectInd;
                op = stage == 2 ? orderStage2Mappings[btnOrders[refBtnInd]] : orderStage3Mappings[btnOrders[refBtnInd]];
            }

            int index = (charToNum(btnChars[refBtnInd]) + 6) % 7;
            Debug.LogFormat("[Binary Tree #{0}] Reference button is {1} {2}({3}).", _moduleId, orderColorNames[btnOrders[refBtnInd]], btnChars[refBtnInd], charToNum(btnChars[refBtnInd]));
            if (btnReverse[refBtnInd])
            {
                index = 6 - index;
                Debug.LogFormat("[Binary Tree #{0}] Text is Silver! Should count in reverse.", _moduleId);
            }

            int correctInd;
            switch (op)
            {
                case 0:
                    {
                        correctInd = preOrder[index];
                        Debug.LogFormat("[Binary Tree #{0}] Correct order is preorder.", _moduleId);
                        Debug.LogFormat("[Binary Tree #{0}] Preorder sequence: {1}{2}{3}{4}{5}{6}{7}.", _moduleId,
                            btnChars[preOrder[0]],
                            btnChars[preOrder[1]],
                            btnChars[preOrder[2]],
                            btnChars[preOrder[3]],
                            btnChars[preOrder[4]],
                            btnChars[preOrder[5]],
                            btnChars[preOrder[6]]);
                        break;
                    }
                case 1:
                    {
                        correctInd = inOrder[index];
                        Debug.LogFormat("[Binary Tree #{0}] Correct order is inorder.", _moduleId);
                        Debug.LogFormat("[Binary Tree #{0}] Inorder sequence: {1}{2}{3}{4}{5}{6}{7}.", _moduleId,
                            btnChars[inOrder[0]],
                            btnChars[inOrder[1]],
                            btnChars[inOrder[2]],
                            btnChars[inOrder[3]],
                            btnChars[inOrder[4]],
                            btnChars[inOrder[5]],
                            btnChars[inOrder[6]]);
                        break;
                    }
                case 2:
                    {
                        correctInd = postOrder[index];
                        Debug.LogFormat("[Binary Tree #{0}] Correct order is postorder.", _moduleId);
                        Debug.LogFormat("[Binary Tree #{0}] Postorder sequence: {1}{2}{3}{4}{5}{6}{7}.", _moduleId,
                            btnChars[postOrder[0]],
                            btnChars[postOrder[1]],
                            btnChars[postOrder[2]],
                            btnChars[postOrder[3]],
                            btnChars[postOrder[4]],
                            btnChars[postOrder[5]],
                            btnChars[postOrder[6]]);
                        break;
                    }
                case 3:
                    {
                        correctInd = index;
                        Debug.LogFormat("[Binary Tree #{0}] Correct order is level order.", _moduleId);
                        Debug.LogFormat("[Binary Tree #{0}] Level Order sequence: {1}{2}{3}{4}{5}{6}{7}.", _moduleId,
                            btnChars[0],
                            btnChars[1],
                            btnChars[2],
                            btnChars[3],
                            btnChars[4],
                            btnChars[5],
                            btnChars[6]);
                        break;
                    }
                case 4:
                    {
                        correctInd = preOrderF[index];
                        Debug.LogFormat("[Binary Tree #{0}] Correct order is right-to-left preorder.", _moduleId);
                        Debug.LogFormat("[Binary Tree #{0}] R-to-L Preorder sequence: {1}{2}{3}{4}{5}{6}{7}.", _moduleId,
                            btnChars[preOrderF[0]],
                            btnChars[preOrderF[1]],
                            btnChars[preOrderF[2]],
                            btnChars[preOrderF[3]],
                            btnChars[preOrderF[4]],
                            btnChars[preOrderF[5]],
                            btnChars[preOrderF[6]]);
                        break;
                    }
                case 5:
                    {
                        correctInd = inOrderF[index];
                        Debug.LogFormat("[Binary Tree #{0}] Correct order is right-to-left inorder.", _moduleId);
                        Debug.LogFormat("[Binary Tree #{0}] R-to-L Inorder sequence: {1}{2}{3}{4}{5}{6}{7}.", _moduleId,
                            btnChars[inOrderF[0]],
                            btnChars[inOrderF[1]],
                            btnChars[inOrderF[2]],
                            btnChars[inOrderF[3]],
                            btnChars[inOrderF[4]],
                            btnChars[inOrderF[5]],
                            btnChars[inOrderF[6]]);
                        break;
                    }
                case 6:
                    {
                        correctInd = postOrderF[index];
                        Debug.LogFormat("[Binary Tree #{0}] Correct order is right-to-left postorder.", _moduleId);
                        Debug.LogFormat("[Binary Tree #{0}] R-to-L Postorder sequence: {1}{2}{3}{4}{5}{6}{7}.", _moduleId,
                            btnChars[postOrderF[0]],
                            btnChars[postOrderF[1]],
                            btnChars[postOrderF[2]],
                            btnChars[postOrderF[3]],
                            btnChars[postOrderF[4]],
                            btnChars[postOrderF[5]],
                            btnChars[postOrderF[6]]);
                        break;
                    }
                case 7:
                    {
                        correctInd = lvlOrderF[index];
                        Debug.LogFormat("[Binary Tree #{0}] Correct order is right-to-left level order.", _moduleId);
                        Debug.LogFormat("[Binary Tree #{0}] R-to-L Level Order sequence: {1}{2}{3}{4}{5}{6}{7}.", _moduleId,
                        btnChars[lvlOrderF[0]],
                        btnChars[lvlOrderF[1]],
                        btnChars[lvlOrderF[2]],
                        btnChars[lvlOrderF[3]],
                        btnChars[lvlOrderF[4]],
                        btnChars[lvlOrderF[5]],
                        btnChars[lvlOrderF[6]]);
                        break;
                    }
                default:
                    {
                        correctInd = 0;
                        Debug.LogFormat("[Binary Tree #{0}] Error: Unexpected order code. Defaulting to 0.", _moduleId);
                        break;
                    }
            }
            Debug.LogFormat("[Binary Tree #{0}] Correct index is #{1}, expected button is {2} {3}.", _moduleId, btnReverse[refBtnInd] ? (7 - index) : index + 1, orderColorNames[btnOrders[correctInd]], btnChars[correctInd]);

            if (correctInd == num)
            {
                ScreenDisplays[stage - 1].text = btnChars[correctInd].ToString();
                ScreenDisplays[stage - 1].color = orderColors[btnOrders[correctInd]];
                prevCorrectInd = correctInd;
                stage++;
                if (stage > 3)
                {
                    Debug.LogFormat("[Binary Tree #{0}] PASSED!", _moduleId);
                    Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, Module.transform);
                    Module.HandlePass();
                    _isSolved = true;
                    ScreenDisplays[3].text = "✓";
                    ScreenDisplays[3].color = Color.green;
                    return;
                }
                Debug.LogFormat("[Binary Tree #{0}] Correct! Current stage: {1}", _moduleId, stage);
            }
            else
            {
                Debug.LogFormat("[Binary Tree #{0}] STRIKE! Incorrect!", _moduleId);
                Module.HandleStrike();
                ScreenDisplays[3].text = "✗";
                ScreenDisplays[3].color = Color.red;
                _isStrike = true;
                strikeMarkTime = Time.time;
            }
        }
    }

    int charToNum(char c)
    {
        if (c >= '0' && c <= '9') return c - '0';
        else return c - 'A' + 10;
    }

    char numToChar(int n)
    {
        if (n < 10) return (char) (n + '0');
        else return (char) (n - 10 + 'A');
    }

    #endregion

    private void Update()
    {
        if (_isStrike)
        {
            if (Time.time - strikeMarkTime > 0.5f)
            {
                ScreenDisplays[3].text = "";
                _isStrike = false;
            }
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"Press a button with “!{0} press 1”, where the buttons are numbered 1–7 in reading order.";
#pragma warning restore 414

    KMSelectable[] ProcessTwitchCommand(string command)
    {
        var m = Regex.Match(command, @"^\s*press\s+([1-7])\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!m.Success)
            return null;
        return new[] { NodeBtns[int.Parse(m.Groups[1].Value) - 1] };
    }
}