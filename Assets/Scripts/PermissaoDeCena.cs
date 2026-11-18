using UnityEngine;
using UnityEngine.Android;
using UnityEngine.Events;

[AddComponentMenu("Permissão de cena")]
public class PermissaoDeCena : MonoBehaviour
{
    private const string PERMISSAO = "com.oculus.permission.USE_SCENE";
    public Behaviour[] ligarQuandoConceder;

    [Header("Eventos")]
    public UnityEvent aoConceder;
    public UnityEvent aoNegar;

    public string Estado { get; private set; } = "por pedir";

    private void Start()
    {
#if UNITY_ANDROID
        if (Permission.HasUserAuthorizedPermission(PERMISSAO))
        {
            Concedida();
            return;
        }

        var cb = new PermissionCallbacks();
        cb.PermissionGranted += _ => Concedida();
        cb.PermissionDenied += _ => Negada();

        Estado = "a pedir…";
        Permission.RequestUserPermission(PERMISSAO, cb);
#else
            Estado = "n/a (editor)";
            Concedida();
#endif
    }

    private void Concedida()
    {
        Estado = "concedida";
        if (ligarQuandoConceder != null)
            foreach (var b in ligarQuandoConceder)
                if (b != null) b.enabled = true;
        aoConceder?.Invoke();
    }

    private void Negada()
    {
        Estado = "NEGADA";
        Debug.LogWarning(
            $"Permissão {PERMISSAO} negada. " +
            "Planos, malha e oclusão vão ficar sem dados — e não dão erro.");
        aoNegar?.Invoke();
    }
}