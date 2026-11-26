using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Hands;
#if UNITY_EDITOR
using UnityEngine.InputSystem;
#endif

public class PlanePokeInteraction : MonoBehaviour
{
    [Header("Objetos")]
    [SerializeField] private GameObject prefab;

    [Header("Deteção")]
    [Tooltip("Raio da esfera à volta da ponta do dedo")]
    [SerializeField] private float raioDedo = 0.012f;
    [Tooltip("Tempo mínimo entre dois toques")]
    [SerializeField] private float intervaloMinimo = 0.5f;
    [SerializeField] private bool mostrarLogs = true;
    
    [Header("Tempo de contacto")]
    [SerializeField] private float tempoNecessario = 1f;
    
    private readonly float[] tempoContacto = new float[2];
    private readonly bool[] toqueConfirmado = new bool[2];

    private readonly List<XRHandSubsystem> subsistemas = new List<XRHandSubsystem>();
    private readonly Collider[] buffer = new Collider[8];

    private readonly Vector3?[] posAnterior = new Vector3?[2];
    private readonly bool[] aTocar = new bool[2];
    private float ultimoToque = -10f;
    private GameObject obj;

    private void Update()
    {
        SubsystemManager.GetSubsystems(subsistemas);
        foreach (XRHandSubsystem s in subsistemas)
        {
            if (!s.running)
                continue;

            VerificarMao(s.leftHand, 0);
            VerificarMao(s.rightHand, 1);
            break;
        }

#if UNITY_EDITOR
        TesteComRato();
#endif
    }

    private void VerificarMao(XRHand mao, int i)
    {
        if (!mao.isTracked ||
            !mao.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose pose))
        {
            posAnterior[i] = null;
            aTocar[i] = false;
            return;
        }

        Vector3 ponta = transform.parent != null
            ? transform.parent.TransformPoint(pose.position)
            : pose.position;

        ARPlane plano = null;

        // Verificar contacto com o plano
        int n = Physics.OverlapSphereNonAlloc(
            ponta, raioDedo, buffer, ~0, QueryTriggerInteraction.Collide);
        for (int k = 0; k < n && plano == null; k++)
            plano = buffer[k].GetComponentInParent<ARPlane>();

        if (plano == null && posAnterior[i].HasValue)
        {
            Vector3 anterior = posAnterior[i].Value;
            Vector3 d = ponta - anterior;
            if (d.sqrMagnitude > 1e-8f &&
                Physics.SphereCast(anterior, raioDedo, d.normalized, out RaycastHit hit,
                    d.magnitude, ~0, QueryTriggerInteraction.Collide))
            {
                plano = hit.collider.GetComponentInParent<ARPlane>();
            }
        }

        posAnterior[i] = ponta;
        
        bool tocou = plano != null;
        
        if (tocou && !aTocar[i])
        {
            tempoContacto[i] = 0f;
            toqueConfirmado[i] = false;
        }
        
        if (tocou && !toqueConfirmado[i])
        {
            tempoContacto[i] += Time.deltaTime;
        
            if (tempoContacto[i] >= tempoNecessario)
            {
                Colocar(
                    plano,
                    ponta,
                    i == 0 ? "Mão esquerda" : "Mão direita"
                );
        
                toqueConfirmado[i] = true;
            }
        }
        
        if (!tocou)
        {
            tempoContacto[i] = 0f;
            toqueConfirmado[i] = false;
        }
        
        aTocar[i] = tocou;
    }

    private void Colocar(ARPlane plano, Vector3 ponto, string origem)
    {
        if (prefab == null)
        {
            Debug.LogWarning("PlanePokeInteraction: falta associar o Prefab no Inspector.");
            return;
        }

        if (Time.time - ultimoToque < intervaloMinimo)
            return;
        ultimoToque = Time.time;

        Vector3 normal = plano.normal;
        Vector3 posicao = ponto - Vector3.Dot(ponto - plano.transform.position, normal) * normal;
        // +0.2f para que o canvas não fique dentro do plano
        posicao.y += 0.2f;

        Quaternion rotacao;

        Camera cam = Camera.main;

        Vector3 direcao = posicao - cam.transform.position;

        direcao = Vector3.ProjectOnPlane(direcao, normal).normalized;

        rotacao = Quaternion.LookRotation(direcao, normal);

        if (obj == null)
            obj = Instantiate(prefab, posicao, rotacao);
        else
            obj.transform.SetPositionAndRotation(posicao, rotacao);

        if (mostrarLogs)
            Debug.Log($"Poke ({origem}) no plano {plano.trackableId} em {posicao}");
    }

#if UNITY_EDITOR
    private void TesteComRato()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        Camera cam = Camera.main;
        if (cam == null)
            return;

        Ray raio = cam.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(raio, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Collide))
        {
            ARPlane plano = hit.collider.GetComponentInParent<ARPlane>();
            if (plano != null)
                Colocar(plano, hit.point, "Rato (Editor)");
        }
    }
#endif
}
