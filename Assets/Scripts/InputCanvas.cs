using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InputCanvas : MonoBehaviour
{
    [Header("Definir Campo Input")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private Button done;
    [SerializeField] private Button cancel;

    [Header("Canvas para Instanciar")]
    [SerializeField] private GameObject canvasPrefab;
    
    private Transform _parent;
    private GameObject _obj;
    public string textToDisplay;
    
    void Start()
    {
        _parent = gameObject.transform.parent;
    }
    
    public void ChangeText(string input)
    {
        textToDisplay = input;
        Debug.Log(textToDisplay);
    }

    public void Done()
    {
        textToDisplay = inputField.text;
        _obj = Instantiate(canvasPrefab, _parent, false);
        _obj.transform.localPosition = Vector3.zero;
        
        TMP_Text text = _obj.GetComponentInChildren<TMP_Text>();

        if (text != null)
            text.text = textToDisplay;
        
        Destroy(gameObject);
    }

    public void Cancel()
    {
        Destroy(gameObject);
    }
}
