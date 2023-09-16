using TMPro;
using UnityEngine;

public class ChatBubble : MonoBehaviour
{
    public static ChatBubble Instance { get; set; }

    [SerializeField] private GameObject callSymbol;
    [SerializeField] private CanvasGroup canvasGroupRight;
    [SerializeField] private GameObject canvasTextRight;
    private TextMeshProUGUI textLog;
    [SerializeField] private float fadeSpeed;
    [SerializeField] private float wordSpeed;

    private string completeText;
    private int index;
    private int indexMax;

    private float timer;
    private char[] charList;
    [SerializeField] private bool quickFinish;
    [SerializeField] private bool clearDia;
    [SerializeField] public bool isWriting { get; set;}

    void Start()
    {
        Instance = this;
    }
    // Start is called before the first frame update
    private void OnEnable()
    {
        callSymbol.SetActive(false);
        canvasGroupRight.alpha = 0f;
    }
    // Update is called once per frame

    public void ShowCallSymbol(bool b)
    {
        callSymbol.SetActive(b);
    }
    public void ShowDialogue(string s, Vector3 vec)
    {
        textLog = canvasTextRight.GetComponent<TextMeshProUGUI>();
        textLog.text = "";
        isWriting = true;

        transform.position = vec;
        //text manupulation
        completeText = s;
        charList = s.ToCharArray();
        index = 0;
        indexMax = charList.Length;
    }


    public void QuickFinishDialogue()
    {
        if (isWriting)
        {
            quickFinish = true;
        }
    }

    public void CloseDialogue()
    {
        clearDia = true;
    }
    void Update()
    {
        if (clearDia == true && !isWriting)
        {
            if (canvasGroupRight.alpha > 0f)
            {
                canvasGroupRight.alpha -= Time.deltaTime * fadeSpeed;
            }
            if (canvasGroupRight.alpha == 0f)
            {
                clearDia = false;
            }
        }

        if (!isWriting)
        {
            return;
        }

        textLog.alpha = 1f;
        if (quickFinish)
        {
            textLog.text = completeText;
            canvasGroupRight.alpha = 1f;
            quickFinish = false;
            isWriting = false;
            return;
        }

        if (canvasGroupRight.alpha < 1f)
        {
            canvasGroupRight.alpha += Time.deltaTime * fadeSpeed;
        }
        if (Time.time > timer && canvasGroupRight.alpha == 1f)
        {
            textLog.text += charList[index];
            index += 1;
            timer = Time.time + wordSpeed;
        }
        if (index == indexMax)
        {
            isWriting = false;
        }

    }

}