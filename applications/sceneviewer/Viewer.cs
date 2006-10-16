using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using libsecondlife;
using sceneviewer.Prims;

namespace sceneviewer
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    partial class Viewer : Microsoft.Xna.Framework.Game
    {
        private SecondLife Client;
        private Dictionary<uint, PrimVisual> Prims;

        // The shader effect that we're loading
        private Effect effect;

        // Variables describing the shapes being drawn
        private VertexDeclaration vertexDeclaration;

        private Camera Camera;
        private Matrix World;
        //private WaterSurface Water;

        // Variables for keeping track of the state of the mouse
        private ButtonState PreviousLeftButton;
        private Vector2 PreviousMousePosition;

        //
        private Timer CameraUpdateTimer;

        public Viewer()
        {
            //this.AllowUserResizing = true;
            this.IsMouseVisible = true;

            Window.ClientSizeChanged += new EventHandler(Window_ClientSizeChanged);
            this.Exiting += new EventHandler<GameEventArgs>(Viewer_Exiting);

            Camera = new Camera(this.Window, new Vector3(0, 0, 40), new Vector3(256, 256, 0));
            PreviousLeftButton = ButtonState.Released;

            Prims = new Dictionary<uint, PrimVisual>();

            Hashtable loginParams = NetworkManager.DefaultLoginValues("Ron", "Hubbard",
                "radishman", "00:00:00:00:00:00", "last", 1, 50, 50, 50, "Win", "0",
                "botmanager", "contact@libsecondlife.org");

            Client = new SecondLife();

            Client.Objects.OnNewPrim += new NewPrimCallback(OnNewPrim);
            Client.Objects.OnPrimMoved += new PrimMovedCallback(OnPrimMoved);
            Client.Objects.OnObjectKilled += new KillObjectCallback(OnObjectKilled);

            if (!Client.Network.Login(loginParams))
            {
                Exit();
            }

            InitializeComponent();
            InitializeTransform();
            InitializeEffect();
            InitializeScene();

            // Start the timer
            CameraUpdateTimer = new Timer(new TimerCallback(SendCameraUpdate), null, 0,
                500);
        }

        void Viewer_Exiting(object sender, GameEventArgs e)
        {
            Client.Network.Logout();
        }

        void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            Camera.UpdateProjection(this.Window);
        }

        void SendCameraUpdate(object obj)
        {
            Client.Avatar.UpdateCamera(false);
        }

        void OnNewPrim(Simulator simulator, PrimObject prim, ulong regionHandle, ushort timeDilation)
        {
            PrimVisual primVisual = PrimVisual.BuildPrimVisual(prim);

            if (primVisual.GetType() == typeof(PrimVisualBox))
            {
                lock (Prims)
                {
                    Prims.Add(prim.LocalID, primVisual);
                }
            }
        }

        void OnPrimMoved(Simulator simulator, PrimUpdate primUpdate, ulong regionHandle, ushort timeDilation)
        {
            if (Prims.ContainsKey(primUpdate.LocalID))
            {
                Prims[primUpdate.LocalID].Update(primUpdate);
            }
            else
            {
                Client.Objects.RequestObject(simulator, primUpdate.LocalID);
            }
        }

        void OnObjectKilled(Simulator simulator, uint localID)
        {
            lock (Prims)
            {
                Prims.Remove(localID);
            }
        }

        private void InitializeTransform()
        {
            World = Matrix.CreateTranslation(Vector3.Zero);
        }

        private void InitializeEffect()
        {
            CompiledEffect compiledEffect = Effect.CompileEffectFromFile(
                "ReallySimpleEffect.fx", null, null,
                CompilerOptions.Debug |
                CompilerOptions.SkipOptimization,
                TargetPlatform.Windows);

            effect = new Effect(graphics.GraphicsDevice,
                compiledEffect.GetShaderCode(), CompilerOptions.None,
                null);
        }

        private void InitializeScene()
        {
            vertexDeclaration = new VertexDeclaration(
                graphics.GraphicsDevice, VertexPositionColor.VertexElements);

            //Water = new WaterSurface(graphics.GraphicsDevice, Camera, new Vector3(0, 0, 1), 0, 128, 256);
        }

        protected override void Update()
        {
            // The time since Update was called last
            float elapsed = (float)ElapsedTime.TotalSeconds;

            MouseState currentState = Mouse.GetState();

            Camera.Zoom = currentState.ScrollWheelValue * 0.005f;

            if (currentState.LeftButton == ButtonState.Pressed &&
                PreviousLeftButton == ButtonState.Pressed)
            {
                Vector2 curMouse = new Vector2(currentState.X, currentState.Y);
                Vector2 deltaMouse = PreviousMousePosition - curMouse;

                Camera.Theta += deltaMouse.X * 0.01f;
                Camera.Phi -= deltaMouse.Y * 0.005f;
                PreviousMousePosition = curMouse;
            }
            // It's implied that the leftPreviousState is unpressed in this situation.
            else if (currentState.LeftButton == ButtonState.Pressed)
            {
                PreviousMousePosition = new Vector2(currentState.X, currentState.Y);
            }

            PreviousLeftButton = currentState.LeftButton;

            // Let the GameComponents update
            UpdateComponents();
        }

        protected override void Draw()
        {
            Matrix ViewProjectionMatrix = Camera.ViewProjectionMatrix;

            // Make sure we have a valid device
            if (!graphics.EnsureDevice())
                return;

            graphics.GraphicsDevice.Clear(Color.CornflowerBlue);
            graphics.GraphicsDevice.BeginScene();

            // Let the GameComponents draw
            DrawComponents();

            //effect.Parameters["WorldViewProj"].SetValue(World * ViewProjectionMatrix);
            //effect.CurrentTechnique = effect.Techniques["TransformTechnique"];
            //effect.CommitChanges();

            graphics.GraphicsDevice.VertexDeclaration = vertexDeclaration;
            graphics.GraphicsDevice.RenderState.CullMode = CullMode.None;
            graphics.GraphicsDevice.RenderState.FillMode = FillMode.WireFrame;
            //graphics.GraphicsDevice.RenderState.MultiSampleAntiAlias = true;

            effect.Begin(EffectStateOptions.Default);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Begin();

                //Water.Prepare();
                //Water.RenderCutter();

                //pass.End();

                //pass.Begin();

                lock (Prims)
                {
                    foreach (PrimVisual prim in Prims.Values)
                    {
                        if (prim.Prim.ParentID != 0)
                        {
                            if (!Prims.ContainsKey(prim.Prim.ParentID))
                            {
                                // We don't have the base position for this child prim, can't render it
                                continue;
                            }
                            else
                            {
                                // Child prim in a linkset

                                // Add the base position of the parent prim and the offset position of this child
                                LLVector3 llBasePosition = Prims[prim.Prim.ParentID].Prim.Position;
                                LLQuaternion llBaseRotation = Prims[prim.Prim.ParentID].Prim.Rotation;

                                Vector3 basePosition = new Vector3(llBasePosition.X, llBasePosition.Y, llBasePosition.Z);

                                Matrix worldOffset = Matrix.CreateTranslation(basePosition);
                                Matrix rootRotation = Matrix.FromQuaternion(new Quaternion(llBaseRotation.X, llBaseRotation.Y,
                                    llBaseRotation.Z, llBaseRotation.W));

                                effect.Parameters["WorldViewProj"].SetValue(prim.Matrix * rootRotation * worldOffset * ViewProjectionMatrix);
                                effect.CommitChanges();

                                graphics.GraphicsDevice.DrawUserPrimitives<VertexPositionColor>(PrimitiveType.TriangleList,
                                    prim.VertexArray.Length / 3, prim.VertexArray);
                            }
                        }
                        else
                        {
                            // Root prim or not part of a linkset

                            effect.Parameters["WorldViewProj"].SetValue(prim.Matrix * ViewProjectionMatrix);
                            effect.CommitChanges();

                            graphics.GraphicsDevice.DrawUserPrimitives<VertexPositionColor>(PrimitiveType.TriangleList,
                                prim.VertexArray.Length / 3, prim.VertexArray);
                        }
                    }
                }

                pass.End();
            }
            effect.End();

            graphics.GraphicsDevice.EndScene();
            graphics.GraphicsDevice.Present();
        }
    }
}