using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;

public class PlaneDebug : MonoBehaviour
{
    [SerializeField] private ARPlaneManager planeManager;

    private float timer;

    void Start()
    {
        var loader = XRGeneralSettings.Instance?
            .Manager?.activeLoader;

        if (loader == null)
        {
            Debug.LogError("Não existe um XR Loader ativo!");
            return;
        }

        var planeSubsystem =
            loader.GetLoadedSubsystem<XRPlaneSubsystem>();

        if (planeSubsystem == null)
        {
            Debug.LogError(
                "XRPlaneSubsystem não foi carregado!"
            );
        }
        else
        {
            Debug.Log(
                "XRPlaneSubsystem carregado com sucesso!"
            );

            Debug.Log(
                "Suporta deteção: " +
                planeSubsystem.subsystemDescriptor.supportsClassification
            );
        }
        
        Debug.Log("Requested Mode: " +
                  planeManager.requestedDetectionMode);

        Debug.Log("Current Mode: " +
                  planeManager.currentDetectionMode);
    }
    
    void Update()
    {
        if (planeManager == null)
        {
            Debug.LogError("ARPlaneManager não está associado!");
            enabled = false;
            return;
        }

        timer += Time.deltaTime;

        if (timer >= 3f)
        {
            timer = 0f;

            Debug.Log(
                "ARPlaneManager ativo: " + planeManager.isActiveAndEnabled
            );

            Debug.Log(
                "Planos detetados: " + planeManager.trackables.count
            );
        }
    }
}