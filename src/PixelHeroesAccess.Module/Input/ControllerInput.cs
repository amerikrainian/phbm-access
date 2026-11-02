namespace PixelHeroesAccess.Module.Input
{
    // The game declares a class named Action in the global namespace.
    using Action = System.Action;

    /// <summary>A gamepad control as the mod binds it: a button, or one direction of a stick or trigger.</summary>
    public enum ControllerInput
    {
        DpadUp,
        DpadDown,
        DpadLeft,
        DpadRight,

        A,
        B,
        X,
        Y,

        LeftShoulder,
        RightShoulder,
        LeftTrigger,
        RightTrigger,

        LeftStickUp,
        LeftStickDown,
        LeftStickLeft,
        LeftStickRight,

        RightStickUp,
        RightStickDown,
        RightStickLeft,
        RightStickRight,

        LeftStickClick,
        RightStickClick,

        Start,
        Back,
    }
}
