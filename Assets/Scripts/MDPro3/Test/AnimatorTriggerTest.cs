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
            Debug.Log(": Parameter name: " + param.name + " Parameter type: " + param.type);

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
