namespace YgomSystem.UI
{
	public class TweenEvent : Tween
	{
		protected override void OnSetValue(float par)
		{
            // Event-only Tween: changes no properties and uses the base onFinished callback.
            // Often used as a delay or chain node in prefabs to trigger the next Tween through onFinished.
        }
	}
}
