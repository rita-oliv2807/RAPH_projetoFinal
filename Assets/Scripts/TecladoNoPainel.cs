using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RAPH.Demos
{
    [AddComponentMenu("RAPH/Teclado no painel")]
    public class TecladoNoPainel : MonoBehaviour
    {
        [Header("Destino")]
        [Tooltip("O campo de texto que estas teclas preenchem.")]
        public TMP_InputField campo;

        [Header("Disposição")]
        [Tooltip("Linhas de teclas. Uma string por linha.")]
        public string[] linhas =
        {
            "1234567890",
            "QWERTYUIOP",
            "ASDFGHJKL",
            "ZXCVBNM",
        };

        [Tooltip("Altura de cada tecla, em unidades do canvas.")]
        public float alturaTecla = 0.055f;
        [Tooltip("Folga entre teclas.")]
        public float folga = 0.008f;
        [Tooltip("Onde começa o teclado, medido do centro do canvas. " +
                 "Deve ficar ABAIXO do retângulo do painel para não tapar " +
                 "os botões — num canvas de 0,5 de altura, algo como −0,28.")]
        public float topo = -0.28f;

        [Header("Comportamento")]
        [Tooltip("Começa escondido e aparece ao tocar no campo de texto.")]
        public bool aparecerAoTocar = true;

        [Header("Cor")]
        public Color corTecla = new Color(0.16f, 0.18f, 0.22f, 0.95f);
        public Color corTexto = Color.white;
        public Color corEspecial = new Color(0.30f, 0.34f, 0.42f, 0.95f);
        public Color corFundo = new Color(0.08f, 0.09f, 0.11f, 0.80f);

        // ── Estado ─────────────────────────────────────────────────────
        private RectTransform _raiz;
        private TMP_FontAsset _fonte;
        private bool _maiusculas = true;
        private readonly List<TextMeshProUGUI> _letras = new();

        // ══════════════════════════════════════════════════════════════

        private void Start()
        {
            if (campo == null)
            {
                Debug.LogWarning("TecladoNoPainel: falta o campo de texto.");
                enabled = false;
                return;
            }
            
            _fonte = campo.textComponent != null ? campo.textComponent.font : null;
            if (_fonte == null) _fonte = TMP_Settings.defaultFontAsset;
            if (_fonte == null)
                Debug.LogWarning("TecladoNoPainel: sem fonte TMP — teclas sem texto.");
            
            campo.shouldHideSoftKeyboard = true;
            campo.shouldHideMobileInput = true;

            Construir();

            if (aparecerAoTocar)
            {
                _raiz.gameObject.SetActive(false);
                campo.onSelect.AddListener(_ => Mostrar());
            }
        }

        public void Mostrar()
        {
            if (_raiz != null) _raiz.gameObject.SetActive(true);
        }

        public void Esconder()
        {
            if (_raiz != null) _raiz.gameObject.SetActive(false);
        }

        // ══════════════════════════════════════════════════════════════
        //  CONSTRUÇÃO
        // ══════════════════════════════════════════════════════════════

        private void Construir()
        {
            int maiorLinha = 0;
            foreach (var l in linhas) maiorLinha = Mathf.Max(maiorLinha, l.Length);
            float largura = maiorLinha * alturaTecla + (maiorLinha - 1) * folga;
            float altura = (linhas.Length + 1) * (alturaTecla + folga);

            var go = new GameObject("Teclado", typeof(RectTransform), typeof(Image));
            _raiz = (RectTransform)go.transform;
            _raiz.SetParent(transform, false);
            _raiz.anchorMin = _raiz.anchorMax = new Vector2(0.5f, 0.5f);
            _raiz.pivot = new Vector2(0.5f, 1f);
            _raiz.anchoredPosition = new Vector2(0f, topo);
            _raiz.sizeDelta = new Vector2(largura + folga * 4f, altura + folga * 3f);

            var fundo = go.GetComponent<Image>();
            fundo.color = corFundo;
            fundo.raycastTarget = false;

            float y = -alturaTecla * 0.5f - folga * 1.5f;
            foreach (var linha in linhas)
            {
                Linha(linha, y);
                y -= alturaTecla + folga;
            }
            
            LinhaEspecial(y, new (string, float, UnityEngine.Events.UnityAction)[]
            {
                ("ABC",    1.7f, Maiusculas),
                ("espaço", 3.4f, () => Escrever(" ")),
                ("apagar", 1.9f, Apagar),
                ("OK",     1.5f, Esconder),
            });
        }
        
        private void LinhaEspecial(float y,
            (string rotulo, float peso, UnityEngine.Events.UnityAction acao)[] teclas)
        {
            int maior = 0;
            foreach (var l in linhas) maior = Mathf.Max(maior, l.Length);
            float total = maior * alturaTecla + (maior - 1) * folga;

            float pesos = 0f;
            foreach (var t in teclas) pesos += t.peso;
            float unidade = (total - folga * (teclas.Length - 1)) / pesos;

            float x = -total * 0.5f;
            foreach (var t in teclas)
            {
                float larg = unidade * t.peso;
                Tecla(t.rotulo, x + larg * 0.5f, y, larg, corEspecial, t.acao);
                x += larg + folga;
            }
        }

        private void Linha(string teclas, float y)
        {
            int n = teclas.Length;
            float total = n * alturaTecla + (n - 1) * folga;
            float x = -total * 0.5f + alturaTecla * 0.5f;

            foreach (char c in teclas)
            {
                char letra = c;
                var rot = Tecla(letra.ToString(), x, y, alturaTecla, corTecla,
                                () => Escrever(_maiusculas
                                    ? letra.ToString().ToUpper()
                                    : letra.ToString().ToLower()));
                if (char.IsLetter(letra) && rot != null) _letras.Add(rot);
                x += alturaTecla + folga;
            }
        }

        private TextMeshProUGUI Tecla(string rotulo, float x, float y, float largura,
                                      Color cor, UnityEngine.Events.UnityAction acao)
        {
            var go = new GameObject("Tecla " + rotulo,
                                    typeof(RectTransform), typeof(Image), typeof(Button));
            var rt = (RectTransform)go.transform;
            rt.SetParent(_raiz, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(largura, alturaTecla);
            rt.anchoredPosition = new Vector2(x, y);

            var img = go.GetComponent<Image>();
            img.color = cor;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(acao);
            
            var cores = btn.colors;
            cores.highlightedColor = new Color(cor.r + 0.20f, cor.g + 0.20f, cor.b + 0.20f, 1f);
            cores.pressedColor = new Color(0.30f, 0.62f, 0.38f, 1f);
            cores.fadeDuration = 0.04f;
            btn.colors = cores;

            if (_fonte == null) return null;

            var t = new GameObject("T", typeof(RectTransform));
            var trt = (RectTransform)t.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            var txt = t.AddComponent<TextMeshProUGUI>();
            txt.font = _fonte;
            txt.text = rotulo;
            txt.color = corTexto;
            txt.alignment = TextAlignmentOptions.Center;
            txt.raycastTarget = false;
            txt.fontSize = rotulo.Length > 1 ? alturaTecla * 0.30f : alturaTecla * 0.55f;
            txt.enableAutoSizing = false;
            return txt;
        }

        // ══════════════════════════════════════════════════════════════

        private void Escrever(string s)
        {
            campo.text += s;
            campo.caretPosition = campo.text.Length;
        }

        private void Apagar()
        {
            if (campo.text.Length == 0) return;
            campo.text = campo.text.Substring(0, campo.text.Length - 1);
            campo.caretPosition = campo.text.Length;
        }

        private void Maiusculas()
        {
            _maiusculas = !_maiusculas;
            foreach (var t in _letras)
                if (t != null) t.text = _maiusculas ? t.text.ToUpper() : t.text.ToLower();
        }
    }
}
