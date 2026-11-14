namespace YgomSystem.UI
{
	public class TweenEvent : Tween
	{
		protected override void OnSetValue(float par)
		{
            // Event-only tween: leave properties unchanged and use the base onFinished callback.
            // Prefabs can use this as a delay or chain node to start the next tween.
        }
	}
}
