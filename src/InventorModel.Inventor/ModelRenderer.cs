using System;
using System.Collections.Generic;
using System.IO;
using Inventor;
using InventorModel.Core.Diagnostics;

namespace InventorModel.Inventor;

public sealed class ModelRenderer
{
    private readonly Application _app;

    public ModelRenderer(Application app)
    {
        _app = app ??
               throw new ArgumentNullException(nameof(app));
    }

    public IReadOnlyList<string> RenderFourViews(
        PartDocument document,
        string directory,
        int width = 800,
        int height = 800)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));
        if (string.IsNullOrWhiteSpace(directory))
            throw new ArgumentException(
                "Render directory is required.",
                nameof(directory));

        Directory.CreateDirectory(directory);
        document.Activate();

        View view = _app.ActiveView;
        Camera camera = view.Camera;
        CameraState state = CameraState.Capture(camera, view);

        var files = new List<string>();

        try
        {
            view.DisplayMode =
                DisplayModeEnum.kShadedWithEdgesRendering;

            Capture(
                camera,
                view,
                directory,
                ViewOrientationTypeEnum.kFrontViewOrientation,
                "front",
                width,
                height,
                files);
            Capture(
                camera,
                view,
                directory,
                ViewOrientationTypeEnum.kTopViewOrientation,
                "top",
                width,
                height,
                files);
            Capture(
                camera,
                view,
                directory,
                ViewOrientationTypeEnum.kRightViewOrientation,
                "right",
                width,
                height,
                files);
            Capture(
                camera,
                view,
                directory,
                ViewOrientationTypeEnum.kIsoTopRightViewOrientation,
                "iso",
                width,
                height,
                files);

            return files;
        }
        finally
        {
            try
            {
                state.Restore(camera, view);
            }
            catch (Exception ex)
            {
                RuntimeLog.Warning(
                    "Inventor.Render",
                    "The user's original Inventor camera/display state could not be fully restored.",
                    ex);
            }
        }
    }

    private static void Capture(
        Camera camera,
        View view,
        string directory,
        ViewOrientationTypeEnum orientation,
        string name,
        int width,
        int height,
        ICollection<string> files)
    {
        camera.Perspective = false;
        camera.ViewOrientationType = orientation;
        camera.Fit();
        camera.ApplyWithoutTransition();
        view.Update();

        string file =
            Path.Combine(
                directory,
                name + ".png");

        view.SaveAsBitmap(
            file,
            width,
            height);

        files.Add(file);
    }

    private sealed class CameraState
    {
        private double EyeX { get; set; }
        private double EyeY { get; set; }
        private double EyeZ { get; set; }

        private double TargetX { get; set; }
        private double TargetY { get; set; }
        private double TargetZ { get; set; }

        private double UpX { get; set; }
        private double UpY { get; set; }
        private double UpZ { get; set; }

        private bool Perspective { get; set; }
        private double PerspectiveAngle { get; set; }
        private double ExtentWidth { get; set; }
        private double ExtentHeight { get; set; }
        private DisplayModeEnum DisplayMode { get; set; }

        public static CameraState Capture(
            Camera camera,
            View view)
        {
            Point eye = camera.Eye;
            Point target = camera.Target;
            UnitVector up = camera.UpVector;

            camera.GetExtents(
                out double extentWidth,
                out double extentHeight);

            return new CameraState
            {
                EyeX = eye.X,
                EyeY = eye.Y,
                EyeZ = eye.Z,
                TargetX = target.X,
                TargetY = target.Y,
                TargetZ = target.Z,
                UpX = up.X,
                UpY = up.Y,
                UpZ = up.Z,
                Perspective = camera.Perspective,
                PerspectiveAngle = camera.PerspectiveAngle,
                ExtentWidth = extentWidth,
                ExtentHeight = extentHeight,
                DisplayMode = view.DisplayMode
            };
        }

        public void Restore(
            Camera camera,
            View view)
        {
            Application application =
                (Application)view.Application;
            TransientGeometry geometry =
                application.TransientGeometry;

            camera.Eye =
                geometry.CreatePoint(
                    EyeX,
                    EyeY,
                    EyeZ);
            camera.Target =
                geometry.CreatePoint(
                    TargetX,
                    TargetY,
                    TargetZ);
            camera.UpVector =
                geometry.CreateUnitVector(
                    UpX,
                    UpY,
                    UpZ);
            camera.Perspective = Perspective;

            if (Perspective)
                camera.PerspectiveAngle =
                    PerspectiveAngle;

            camera.SetExtents(
                ExtentWidth,
                ExtentHeight);
            camera.ApplyWithoutTransition();

            view.DisplayMode = DisplayMode;
            view.Update();
        }
    }
}
