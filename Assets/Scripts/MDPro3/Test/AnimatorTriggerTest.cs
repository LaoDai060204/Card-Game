using UnityEngine;

[RequireComponent(typeof(Animator))]
public class AnimatorTriggerTest : MonoBehaviour
{
    public string trigger;
    public bool test;

    private Animator animator;

    private void Start()
    {
        animator = transform.GetComponent<Animator>();
        AnimatorControllerParameter[] parameters = animator.parameters;
        foreach (var param in parameters)
            Debug.Log(": 参数名称：" + param.name + " 参数类型：" + param.type);

    }

    private void Update()
    {
        if (test)
        {
            test = false;
            animator.SetTrigger(trigger);
        }
    }
}
