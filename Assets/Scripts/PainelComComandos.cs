using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.XR;

namespace RAPH.Demos
{
    /// </summary>
    [AddComponentMenu("RAPH/Painel operado pelos comandos")]
    public class PainelComComandos : MonoBehaviour
    {
        [Header("Alvo")]
        [Tooltip("O Canvas em World Space. Precisa de um Graphic Raycaster.")]
        public Canvas painel;

        [Header("Material")]
        [Tooltip("Qualquer material do URP. Sem isto o raio sai cor-de-rosa " +
                 "no build — e só no build.")]
        public Material materialBase;

        [Header("Raio")]
        [Range(0.5f, 10f)] public float alcance = 5f;
        [Range(0.001f, 0.02f)] public float espessura = 0.004f;
        public Color corNormal = new Color(0.85f, 0.85f, 0.90f);
        public Color corSobreAlvo = new Color(0.30f, 0.85f, 0.40f);

        [Header("Comando")]
        [Tooltip("Mão a usar. Se não estiver rastreada, cai na outra.")]
        public bool maoDireita = true;

        [Header("Diagnóstico")]
        public bool mostrarDiagnostico = true;

        // ── Estado ─────────────────────────────────────────────────────
        private Camera _olho;
        private Transform _espaco;
        private string _nomeEspaco = "—";
        private GraphicRaycaster _raycaster;
        private LineRenderer _linha;

        private PointerEventData _evento;
        private readonly List<RaycastResult> _resultados = new();

        private GameObject _sobre;        // onde o raio está agora
        private GameObject _premido;      // onde o gatilho foi premido
        private bool _gatilho, _gatilhoAntes;
        private Vector2 _ecraAntes;
        private string _estado = "—";

        // ══════════════════════════════════════════════════════════════
        //  MATERIAIS
        // ══════════════════════════════════════════════════════════════
        // `Shader.Find` devolve `null` no dispositivo, porque um shader
        // que nenhum componente da cena referencie é removido do build.
        // Um material arrastado para um campo é a única via segura.

        private Material NovaMaterial(Color c)
        {
            if (materialBase != null) return new Material(materialBase) { color = c };

            var rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null)
                return new Material(rp.defaultMaterial) { color = c };

            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            return sh != null ? new Material(sh) { color = c } : null;
        }

        // ══════════════════════════════════════════════════════════════

        private void Awake()
        {
            _olho = Camera.main;

            if (painel != null)
            {
                // Um Canvas em World Space precisa de Event Camera, ou o
                // GraphicRaycaster não sabe projetar nada.
                painel.worldCamera = _olho;
                _raycaster = painel.GetComponent<GraphicRaycaster>();
                if (_raycaster == null) _raycaster = painel.gameObject.AddComponent<GraphicRaycaster>();
            }

            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem));

            CriarLinha();
        }

        private void CriarLinha()
        {
            var g = new GameObject("Raio do comando");
            _linha = g.AddComponent<LineRenderer>();
            _linha.useWorldSpace = true;
            _linha.positionCount = 2;
            _linha.startWidth = _linha.endWidth = espessura;
            _linha.numCapVertices = 4;
            var m = NovaMaterial(corNormal);
            if (m != null) _linha.material = m;
        }

        private Transform Espaco()
        {
            if (_espaco != null || _olho == null) return _espaco;
            for (Transform t = _olho.transform.parent; t != null; t = t.parent)
                if (t.name.Replace(" ", "").ToLower().Contains("cameraoffset"))
                { _espaco = t; _nomeEspaco = t.name; return _espaco; }
            _espaco = _olho.transform.parent;
            _nomeEspaco = _espaco != null ? _espaco.name + " (pai)" : "NENHUM";
            return _espaco;
        }

        // ══════════════════════════════════════════════════════════════
        //  LER O COMANDO
        // ══════════════════════════════════════════════════════════════
        //
        // `UnityEngine.XR.InputDevices` já vem no motor e funciona por
        // cima do OpenXR.

        private static readonly List<InputDevice> _disp = new();

        private bool LerComando(out Vector3 pos, out Quaternion rot, out bool gatilho)
        {
            pos = Vector3.zero; rot = Quaternion.identity; gatilho = false;

            XRNode[] ordem = maoDireita
                ? new[] { XRNode.RightHand, XRNode.LeftHand }
                : new[] { XRNode.LeftHand, XRNode.RightHand };

            foreach (var no in ordem)
            {
                InputDevices.GetDevicesAtXRNode(no, _disp);
                foreach (var d in _disp)
                {
                    if (!d.isValid) continue;
                    
                    Vector3 p = default; Quaternion r = default;
                    bool temPose = d.TryGetFeatureValue(CommonUsages.devicePosition, out p)
                                 & d.TryGetFeatureValue(CommonUsages.deviceRotation, out r);
                    if (!temPose) continue;

                    bool t = false;
                    if (!d.TryGetFeatureValue(CommonUsages.triggerButton, out t))
                    {
                        // Alguns perfis não expõem o botão, só o eixo.
                        float eixo;
                        if (d.TryGetFeatureValue(CommonUsages.trigger, out eixo)) t = eixo > 0.6f;
                    }

                    pos = p; rot = r; gatilho = t;
                    _estado = no == XRNode.RightHand ? "comando direito" : "comando esquerdo";
                    return true;
                }
            }
            _estado = "sem comando rastreado";
            return false;
        }

        // ══════════════════════════════════════════════════════════════

        private void Update()
        {
            if (_olho == null || painel == null || _raycaster == null) return;

            if (!LerComando(out Vector3 pL, out Quaternion rL, out bool gat))
            {
                _linha.enabled = false;
                Sair();
                return;
            }
            _linha.enabled = true;

            var esp = Espaco();
            Vector3 origem = esp != null ? esp.TransformPoint(pL) : pL;
            Quaternion rotacao = esp != null ? esp.rotation * rL : rL;
            Vector3 direcao = rotacao * Vector3.forward;

            _gatilhoAntes = _gatilho;
            _gatilho = gat;
            
            Vector3 ponto;
            bool noPlano = PlanoDoPainel(origem, direcao, out ponto);

            _linha.SetPosition(0, origem);
            _linha.SetPosition(1, origem + direcao * alcance);

            if (!noPlano) { Pintar(corNormal); Sair(); return; }

            // ── Ponto de ecrã, que é o que a UI percebe ────────────
            Vector2 ecra = _olho.WorldToScreenPoint(ponto);

            _evento ??= new PointerEventData(EventSystem.current);
            _evento.Reset();
            _evento.position = ecra;
            _evento.delta = ecra - _ecraAntes;
            _evento.scrollDelta = Vector2.zero;
            _evento.button = PointerEventData.InputButton.Left;
            _ecraAntes = ecra;

            _resultados.Clear();
            _raycaster.Raycast(_evento, _resultados);
            GameObject alvo = _resultados.Count > 0 ? _resultados[0].gameObject : null;
            if (_resultados.Count > 0) _evento.pointerCurrentRaycast = _resultados[0];
            
            if (alvo != null) _linha.SetPosition(1, ponto);

            Pintar(alvo != null ? corSobreAlvo : corNormal);
            Hover(alvo);
            Premir(alvo);
            Arrastar();
        }
        
        private bool PlanoDoPainel(Vector3 origem, Vector3 direcao, out Vector3 ponto)
        {
            ponto = Vector3.zero;
            var rt = painel.transform as RectTransform;
            if (rt == null) return false;

            Vector3 normal = rt.forward;
            float denom = Vector3.Dot(normal, direcao);
            if (Mathf.Abs(denom) < 1e-6f) return false;            // paralelo

            float t = Vector3.Dot(rt.position - origem, normal) / denom;
            if (t < 0f || t > alcance) return false;               // atrás, ou longe

            ponto = origem + direcao * t;
            return true;
        }

        // ══════════════════════════════════════════════════════════════
        //  EVENTOS
        // ══════════════════════════════════════════════════════════════

        private void Hover(GameObject alvo)
        {
            if (alvo == _sobre) return;
            if (_sobre != null)
                ExecuteEvents.ExecuteHierarchy(_sobre, _evento, ExecuteEvents.pointerExitHandler);
            _sobre = alvo;
            if (_sobre != null)
                ExecuteEvents.ExecuteHierarchy(_sobre, _evento, ExecuteEvents.pointerEnterHandler);
        }

        private void Premir(GameObject alvo)
        {
            if (_gatilho && !_gatilhoAntes && alvo != null)
            {
                _evento.pressPosition = _evento.position;
                _evento.pointerPressRaycast = _evento.pointerCurrentRaycast;
                _premido = ExecuteEvents.ExecuteHierarchy(alvo, _evento, ExecuteEvents.pointerDownHandler)
                        ?? ExecuteEvents.GetEventHandler<IPointerClickHandler>(alvo);
                _evento.pointerPress = _premido;
                _evento.rawPointerPress = alvo;
                _evento.dragging = false;

                var arr = ExecuteEvents.GetEventHandler<IDragHandler>(alvo);
                _evento.pointerDrag = arr;
                if (arr != null)
                {
                    ExecuteEvents.Execute(arr, _evento, ExecuteEvents.beginDragHandler);
                    _evento.dragging = true;
                }
            }
            else if (!_gatilho && _gatilhoAntes)
            {
                if (_evento.pointerDrag != null && _evento.dragging)
                {
                    ExecuteEvents.Execute(_evento.pointerDrag, _evento, ExecuteEvents.endDragHandler);
                    _evento.dragging = false;
                    _evento.pointerDrag = null;
                }
                if (_premido != null)
                {
                    ExecuteEvents.Execute(_premido, _evento, ExecuteEvents.pointerUpHandler);
                    // Só é clique se largou em cima do mesmo objeto.
                    var alvoClique = alvo != null
                        ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(alvo) : null;
                    if (alvoClique == _premido)
                        ExecuteEvents.Execute(_premido, _evento, ExecuteEvents.pointerClickHandler);
                    _premido = null;
                    _evento.pointerPress = null;
                }
            }
        }
        
        private void Arrastar()
        {
            if (_evento.dragging && _evento.pointerDrag != null)
                ExecuteEvents.Execute(_evento.pointerDrag, _evento, ExecuteEvents.dragHandler);
        }

        private void Sair()
        {
            if (_sobre != null && _evento != null)
                ExecuteEvents.ExecuteHierarchy(_sobre, _evento, ExecuteEvents.pointerExitHandler);
            _sobre = null;
        }

        private void Pintar(Color c)
        {
            if (_linha != null && _linha.material != null) _linha.material.color = c;
        }

        // ══════════════════════════════════════════════════════════════

        private void OnDisable() => Sair();
        
        private void OnDestroy()
        {
            if (_linha != null) Destroy(_linha.gameObject);
        }

        private void OnGUI()
        {
            if (!mostrarDiagnostico || !Application.isEditor) return;
            GUILayout.Label($"{_estado} · espaço: {_nomeEspaco} · " +
                            $"sobre: {(_sobre != null ? _sobre.name : "—")} · " +
                            $"gatilho: {(_gatilho ? "premido" : "solto")}");
        }
    }
}
