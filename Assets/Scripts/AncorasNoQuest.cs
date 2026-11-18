using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[AddComponentMenu("Âncoras persistentes no Quest")]
[RequireComponent(typeof(ARAnchorManager))]
public class AncorasNoQuest : MonoBehaviour
{
    [Header("Colocação")]
    [SerializeField] private GameObject canvasPrefab;
    [Range(0.3f, 2f)] public float distancia = 0.6f;

    [Header("Painel")]
    public bool painelSegueOlhar = true;
    [Range(0.6f, 2.5f)] public float distanciaPainel = 1.1f;

    private ARAnchorManager _ancoras;
    private Camera _olho;
    private TextMesh _texto;
    private Transform _painel;

    private readonly List<ARAnchor> _naSessao = new();
    private readonly List<SerializableGuid> _guardados = new();

    private string _ultimaAcao = "—";
    private string _comandos = "––";
    private string _ultimoId = "—";
    private int _carregadas, _falhadas;
    private bool _ocupado;

    private const string P_N = "TP3_n";
    private const string P_ID = "TP3_id_";
    
    // FPS
    private float _fps;
    private float _timer = 1f;
    private int _frames;

    private void Awake()
    {
        _ancoras = GetComponent<ARAnchorManager>();
        _olho = Camera.main;
        
        // Passthrough
        if (_olho != null)
        {
            _olho.clearFlags = CameraClearFlags.SolidColor;
            _olho.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }

        CriarPainel();
        LerIdentificadoresGuardados();
    }

    private void Update()
    {
        Comandos();
        MoverPainel();
        Pintar();
        
        // FPS
        _frames++;
        _timer -= Time.unscaledDeltaTime;

        if (_timer <= 0f)
        {
            _fps = _frames / (1f - _timer);

            _frames = 0;
            _timer = 1f;
        }
    }

    //  Comandos
    private bool _gatilhoAntes, _aAntes, _bAntes, _xAntes;

    private void Comandos()
    {
        bool grip = false, a = false, b = false, x = false;

        var dir = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        var esq = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        if (dir.isValid)
        {
            dir.TryGetFeatureValue(CommonUsages.gripButton, out grip);
            dir.TryGetFeatureValue(CommonUsages.primaryButton, out a);
            dir.TryGetFeatureValue(CommonUsages.secondaryButton, out b);
        }
        if (esq.isValid)
            esq.TryGetFeatureValue(CommonUsages.primaryButton, out x);

        if (Tecla(KeyCode.Space)) grip = true;
        if (Tecla(KeyCode.G)) a = true;
        if (Tecla(KeyCode.L)) b = true;
        if (Tecla(KeyCode.E)) x = true;

        if (grip && !_gatilhoAntes) _ = Colocar();
        if (a && !_aAntes) _ = GuardarTodas();
        if (b && !_bAntes) _ = CarregarGuardadas();
        if (x && !_xAntes) _ = ApagarTudo();

        _gatilhoAntes = grip; _aAntes = a; _bAntes = b; _xAntes = x;
    }

    private bool _tecladoOk = true;
    private bool Tecla(KeyCode k)
    {
        if (!_tecladoOk) return false;
        try { return Input.GetKeyDown(k); }
        catch { _tecladoOk = false; return false; }
    }

    private async Awaitable Colocar()
    {
        if (_ocupado || _olho == null) return;
        _ocupado = true;

        Vector3 p = _olho.transform.position + _olho.transform.forward * distancia;
        Quaternion r = Quaternion.Euler(0f, _olho.transform.eulerAngles.y, 0f);

        _ultimaAcao = "a criar âncora…";

        var res = await _ancoras.TryAddAnchorAsync(new Pose(p, r));

        if (res.status.IsSuccess())
        {
            ARAnchor a = res.value;
            _naSessao.Add(a);
            Marca(a.transform);
            _ultimaAcao = $"âncora criada ({_naSessao.Count} na sessão)";
            _ultimoId = Curto(a.trackableId);
        }
        else
        {
            _ultimaAcao = $"✘ criar falhou — código {res.status.nativeStatusCode}";
        }
        _ocupado = false;
    }

    private async Awaitable GuardarTodas()
    {
        if (_ocupado || _naSessao.Count == 0) return;
        _ocupado = true;
        _ultimaAcao = "a guardar…";

        int ok = 0;
        foreach (var a in _naSessao)
        {
            if (a == null) continue;
            var res = await _ancoras.TrySaveAnchorAsync(a);
            if (res.status.IsSuccess())
            {
                SerializableGuid id = res.value;
                if (!_guardados.Contains(id)) _guardados.Add(id);
                _ultimoId = Curto(id);
                ok++;
            }
        }

        EscreverIdentificadores();
        _ultimaAcao = ok > 0
            ? $"guardadas {ok} · identificadores em PlayerPrefs"
            : "✘ guardar falhou";
        _ocupado = false;
    }

    private async Awaitable CarregarGuardadas()
    {
        if (_ocupado) return;
        if (_guardados.Count == 0) { _ultimaAcao = "não há nada guardado"; return; }

        _ocupado = true;
        _carregadas = 0; _falhadas = 0;
        _ultimaAcao = "a relocalizar e a carregar…";

        foreach (var id in _guardados)
        {
            var res = await _ancoras.TryLoadAnchorAsync(id);
            if (res.status.IsSuccess())
            {
                _carregadas++;
                Marca(res.value.transform);
            }
            else
            {
                _falhadas++;
            }
        }

        _ultimaAcao = _falhadas == 0
            ? $"carregadas {_carregadas}"
            : $"carregadas {_carregadas} · {_falhadas} não localizáveis AQUI";
        _ocupado = false;
    }

    private async Awaitable ApagarTudo()
    {
        if (_ocupado) return;
        _ocupado = true;
        _ultimaAcao = "a apagar…";

        foreach (var id in _guardados)
            await _ancoras.TryEraseAnchorAsync(id);

        _guardados.Clear();
        EscreverIdentificadores();

        foreach (var a in _naSessao) if (a != null) Destroy(a.gameObject);
        _naSessao.Clear();
        _carregadas = _falhadas = 0;
        _ultimoId = "—";
        _ultimaAcao = "apagado — no sistema E na nossa lista";
        _ocupado = false;
    }

    private void EscreverIdentificadores()
    {
        PlayerPrefs.SetInt(P_N, _guardados.Count);
        for (int i = 0; i < _guardados.Count; i++)
            PlayerPrefs.SetString(P_ID + i, _guardados[i].guid.ToString());
        PlayerPrefs.Save();
    }

    private void LerIdentificadoresGuardados()
    {
        _guardados.Clear();
        int n = PlayerPrefs.GetInt(P_N, 0);
        for (int i = 0; i < n; i++)
        {
            if (Guid.TryParse(PlayerPrefs.GetString(P_ID + i, ""), out Guid g))
                _guardados.Add(new SerializableGuid(g));
        }
        if (_guardados.Count > 0)
            _ultimaAcao = $"{_guardados.Count} identificador(es) em memória — carrega em L";
    }

    private void CriarPainel()
    {
        var go = new GameObject("Painel");
        _painel = go.transform;

        var t = go.AddComponent<TextMesh>();
        t.font = FonteDoMotor();
        if (t.font != null)
            go.GetComponent<MeshRenderer>().material = t.font.material;
        t.characterSize = 0.012f;
        t.fontSize = 90;
        t.anchor = TextAnchor.UpperLeft;
        t.alignment = TextAlignment.Left;
        t.color = Color.white;
        _texto = t;
    }

    private static Font FonteDoMotor()
    {
        foreach (var nome in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
        {
            var f = Resources.GetBuiltinResource<Font>(nome);
            if (f != null) return f;
        }
        return null;
    }

    private void MoverPainel()
    {
        if (_painel == null || _olho == null || !painelSegueOlhar) return;
        Transform o = _olho.transform;
        _painel.position = o.position + o.forward * distanciaPainel
                                      + o.right * -0.28f + o.up * 0.16f;
        _painel.rotation = Quaternion.LookRotation(_painel.position - o.position, Vector3.up);
    }

    private void Pintar()
    {
        if (_texto == null) return;
        _texto.text =
            $"FPS:  {_fps.ToString("F0")}\n" +
            "─────────\n" +
            $"nesta sessão ......... {_naSessao.Count}\n" +
            $"identificadores meus . {_guardados.Count}\n" +
            $"carregadas ........... {_carregadas}\n" +
            $"não localizáveis ..... {_falhadas}\n" +
            $"último id ............ {_ultimoId}\n" +
            "─────────\n" +
            $"{_ultimaAcao}\n" +
            "\n" +
            "grip: colocar   A: guardar\n" +
            "B: carregar        X: apagar";
    }

    private void Marca(Transform pai)
    {
        GameObject obj = Instantiate(canvasPrefab, pai, false);
        obj.transform.localPosition = Vector3.zero;
    }

    private static string Curto(SerializableGuid g)
    {
        string s = g.ToString();
        return s.Length > 13 ? s.Substring(0, 13) + "…" : s;
    }

    private static string Curto(TrackableId t)
    {
        string s = t.ToString();
        return s.Length > 13 ? s.Substring(0, 13) + "…" : s;
    }
}