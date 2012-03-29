/***************************************************************************************
*  Author: 	Toni Schmidt
*  Mail:	toni.schmidt@uni-konstanz.de
*  TUIO 1.1 implementation: Martin Kaltenbrunner <martin@tuio.org>
*
*		Project Squidy, http://www.squidy-lib.de
*		Human-Computer Interaction Group
*		University of Konstanz, Germany
*		http://hci.uni-konstanz.de
*		http://sourceforge.net/projects/squidy-lib/
*
*  Copyright © 2009, Human-Computer Interaction Group, University of Konstanz, Germany
*  
*  This file is part of SurfaceToTUIO.
*
*  SurfaceToTUIO is free software: you can redistribute it and/or modify
*  it under the terms of the GNU Lesser General Public License as published by
*  the Free Software Foundation, either version 3 of the License, or
*  (at your option) any later version.
*
*  SurfaceToTUIO is distributed in the hope that it will be useful,
*  but WITHOUT ANY WARRANTY; without even the implied warranty of
*  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*  GNU Lesser General Public License for more details.
*
*  You should have received a copy of the GNU Lesser General Public License
*  along with SurfaceToTUIO.  If not, see <http://www.gnu.org/licenses/>.
*
***************************************************************************************/

/*
 * Heavily modified by Julian Stahnke, FH Potsdam, 12 October 2010
 * The last finger now disappears correctly when removed from Surface
 * Fiducials and fingers work at the same time, even in Flash
 * http://github.com/touchcoder/SurfaceToTUIO
 * 
*/

using System;
//using System.Drawing;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input.Manipulations;
using System.Diagnostics;
using Microsoft.Surface;
using Microsoft.Surface.Core;
using Microsoft.Xna.Framework;
//using Microsoft.Xna.Framework.Audio;
//using Microsoft.Xna.Framework.Content;
//using Microsoft.Xna.Framework.Graphics;
//using Microsoft.Xna.Framework.Input;
//using Microsoft.Xna.Framework.Storage;
using OSC.NET;
//using Microsoft.Surface.Core.Manipulations;

namespace SurfaceToTUIO
{
    /// <summary>
    /// This is the main type for your application.
    /// </summary>
    public class App1 : Microsoft.Xna.Framework.Game
    {
        private OSCTransmitter _OSCSender { get; set; }

        private TouchTarget contactTarget;
        private UserOrientation currentOrientation = UserOrientation.Bottom;
        private Color backgroundColor = new Color(81, 81, 81);
        private bool applicationLoadCompleteSignalled;
        private Matrix screenTransform = Matrix.Identity;
        ReadOnlyTouchPointCollection previousContacts;
        // application state: Activated, Previewed, Deactivated,
        // start in Activated state
        private bool isApplicationActivated = true;
        private bool isApplicationPreviewed;
        private int _Frame { get; set; }

        // Dictionary containing the Manipulation Data for a single Contact
        // This is used for getting the Contact velocity from Surface SDK
        //private Dictionary<int, Affine2DOperationDeltaEventArgs> _contactManipulationData = new Dictionary<int, Affine2DOperationDeltaEventArgs>();
        private Dictionary<int, Manipulation2DDeltaEventArgs> _contactManipulationData = new Dictionary<int, Manipulation2DDeltaEventArgs>();

        // Dictionary containing the Manipulation Processor for a single Contact
        // This is used for getting the Contact velocity from Surface SDK
        //private Dictionary<int, Affine2DManipulationProcessor> _contactProcessors = new Dictionary<int, Affine2DManipulationProcessor>();
        private Dictionary<int, ManipulationProcessor2D> _contactProcessors = new Dictionary<int, ManipulationProcessor2D>();

        // History Dictionaries
        // These are used for manually computing a Contact's Angular Velocity, Angular Acceleration and Movement Acceleration
        private Dictionary<int, long> _manipulationDataTimestamp = new Dictionary<int, long>();
        private Dictionary<int, long> _angularVelocityTimestamp = new Dictionary<int, long>();
        private Dictionary<int, Vector2> _lastVelocity = new Dictionary<int, Vector2>();
        private Dictionary<int, float> _lastAngularVelocity = new Dictionary<int, float>();

        /// <summary>
        /// The target receiving all surface input for the application.
        /// </summary>
        protected TouchTarget ContactTarget
        {
            get { return contactTarget; }
        }


        #region OnAffine2DManipulationStarted

        /// <summary>
        /// Unused
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAffine2DManipulationStarted(object sender, Manipulation2DStartedEventArgs e)
        {

        }
        #endregion

        #region OnAffine2DDelta
        /// <summary>
        /// Extract the Manipulation Data and write it into our Dictionary
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAffine2DDelta(object sender, Manipulation2DDeltaEventArgs e)
        {

            ReadOnlyTouchPointCollection currentContacts = contactTarget.GetState();
            foreach (TouchPoint c in currentContacts)
            {
                if (c.X == e.OriginX && c.Y == e.OriginY)
                {
                    // BugFix
                    // Apparently this event handling is not thread-safe
                    // Once the surface is reporting a bunch of touches (~30 in testing)
                    // It will try to add a moving touch multiple times to this dictionary.
                    // 
                    // I've fixed this by only taking the last touch
                    // and just skip all the inbetween touches.
                    _contactManipulationData[c.Id] = e;
                }
            }            
        }
        #endregion

        #region OnAffine2DManipulationCompleted
        /// <summary>
        /// Unused
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnAffine2DManipulationCompleted(object sender, Manipulation2DCompletedEventArgs e)
        {
        }
        #endregion

        /// <summary>
        /// Default constructor.
        /// </summary>
        public App1()
        {
            Program.PositionWindow(Window);

            //Form form = (Form)Form.FromHandle(Window.Handle);
            
        }

        #region Basic Functionality
        /// <summary>
        /// Allows the app to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            SetWindowOnSurface();
            InitializeSurfaceInput();

            // Set the application's orientation based on the current launcher orientation
            currentOrientation = ApplicationServices.InitialOrientation;

            // Subscribe to surface application activation events
            ApplicationServices.WindowInteractive += OnApplicationActivated;
            //ApplicationLauncher.ApplicationActivated += OnApplicationActivated;

            ApplicationServices.WindowNoninteractive += OnApplicationPreviewed;
            //ApplicationLauncher.ApplicationPreviewed += OnApplicationPreviewed;

            ApplicationServices.WindowUnavailable += OnApplicationPreviewed;
            //ApplicationLauncher.ApplicationDeactivated += OnApplicationDeactivated;

            // Setup the UI to transform if the UI is rotated.
            //if (currentOrientation == UserOrientation.Top)
            //{
                // Create a rotation matrix to orient the screen so it is viewed correctly when the user orientation is 180 degress different.
                //Matrix rotation = Matrix.CreateRotationZ(MathHelper.ToRadians(180));
                //Matrix translation = Matrix.CreateTranslation(graphics.GraphicsDevice.Viewport.Width, graphics.GraphicsDevice.Viewport.Height, 0);

                //screenTransform = rotation * translation;
            //}

            base.Initialize();
            //int port = 0;
            //bool parseSucc = int.TryParse(ConfigurationSettings.AppSettings["RemotePort"], out port);
            _OSCSender = new OSCTransmitter(Properties.Settings.Default.RemoteHost, Properties.Settings.Default.RemotePort);
            //_OSCSender = new OSCTransmitter("127.0.0.1", 3333);
            _Frame = 0;
        }

        /// <summary>
        /// Moves and sizes the window to cover the input surface.
        /// </summary>
        private void SetWindowOnSurface()
        {
            System.Diagnostics.Debug.Assert(Window.Handle != System.IntPtr.Zero,
                "Window initialization must be complete before SetWindowOnSurface is called");
            if (Window.Handle == System.IntPtr.Zero)
                return;

            // We don't want to run in full-screen mode because we need
            // overlapped windows, so instead run in windowed mode
            // and resize to take up the whole surface with no border.

            // Make sure the graphics device has the correct back buffer size.
//#warning Properties about the device are now on the InteractiveSurfaceDevice class. Check back.
            InteractiveSurfaceDevice interactiveSurface = InteractiveSurface.PrimarySurfaceDevice;
            Console.WriteLine("Bottom: " + interactiveSurface.Bottom.ToString() + "\nTop: " 
                + interactiveSurface.Top.ToString() + "\nLeft: " 
                + interactiveSurface.Left.ToString() + "\nRight: " 
                + interactiveSurface.Right.ToString());
        }

        /// <summary>
        /// Initializes the surface input system. This should be called after any window
        /// initialization is done, and should only be called once.
        /// </summary>
        private void InitializeSurfaceInput()
        {
            System.Diagnostics.Debug.Assert(Window.Handle != System.IntPtr.Zero,
                "Window initialization must be complete before InitializeSurfaceInput is called");
            if (Window.Handle == System.IntPtr.Zero)
                return;
            System.Diagnostics.Debug.Assert(contactTarget == null,
                "Surface input already initialized");
            if (contactTarget != null)
                return;

            // Create a target for surface input.
            //contactTarget = new TouchTarget(Window.Handle, EventThreadChoice.OnBackgroundThread);
            contactTarget = new TouchTarget(System.IntPtr.Zero, true);
            contactTarget.EnableInput();
        }

        /// <summary>
        /// Unload your graphics content.
        /// </summary>
        protected override void UnloadContent()
        {
            Content.Unload();
        }

        /// <summary>
        /// Main application Logic
        /// We retrieve the current Contacts and send TUIO messages accordingly
        /// You can specify via appConfig if you want a Finger Contact to be sent as /tuio/2Dcur or /tuio/2Dblb
        /// Sending it as /tuio/2Dblb conserves the Contact size, rotation, angular velocity and angular acceleration
        /// </summary>
        protected override void Update(GameTime gameTime)
        {
            if (isApplicationActivated || isApplicationPreviewed)
            {
                _contactManipulationData.Clear();

                // Want to identify all the contacts added or removed since the last update.
                List<TouchPoint> addedContacts = new List<TouchPoint>();
                List<TouchPoint> removedContacts = new List<TouchPoint>();
                List<TouchPoint> changedContacts = new List<TouchPoint>();
                List<TouchPoint> aliveContacts = new List<TouchPoint>();

                List<TouchPoint> addedFingers = new List<TouchPoint>();
                List<TouchPoint> removedFingers = new List<TouchPoint>();
                List<TouchPoint> changedFingers = new List<TouchPoint>();
                List<TouchPoint> aliveFingers = new List<TouchPoint>();

                List<TouchPoint> addedTags = new List<TouchPoint>();
                List<TouchPoint> removedTags = new List<TouchPoint>();
                List<TouchPoint> changedTags = new List<TouchPoint>();
                List<TouchPoint> aliveTags = new List<TouchPoint>();

                List<TouchPoint> addedBlobs = new List<TouchPoint>();
                List<TouchPoint> removedBlobs = new List<TouchPoint>();
                List<TouchPoint> changedBlobs= new List<TouchPoint>();
                List<TouchPoint> aliveBlobs = new List<TouchPoint>();

                ReadOnlyTouchPointCollection currentContacts = contactTarget.GetState();

                // Write all unactive previous Contacts into the according removed-Lists
                if (previousContacts != null) {
                    foreach (TouchPoint contact in previousContacts) {
                        //Console.WriteLine("Touch: " + contact.X + ", " + contact.Y);
                        TouchPoint c = null;
                        currentContacts.TryGetTouchPointFromId(contact.Id, out c);
                        if (c == null) {
                            removedContacts.Add(contact);
                            if (contact.IsFingerRecognized && (Properties.Settings.Default.SendFingersAsBlobs == false || Properties.Settings.Default.SendFingersAlsoAsBlobs == true)) {
                                Console.WriteLine("Removing Finger: " + contact.Id);
                                removedFingers.Add(contact);
                            }
                            if (contact.IsFingerRecognized && (Properties.Settings.Default.SendFingersAsBlobs == true || Properties.Settings.Default.SendFingersAlsoAsBlobs == true)) {
                                removedBlobs.Add(contact);
                            } else if (contact.IsTagRecognized) {
                                removedTags.Add(contact);
                            } else {
                                removedBlobs.Add(contact);
                            }
                        }
                    }

                    // Throw away unused Manipulation Processors
                    cleanManipulationProcessorList(removedContacts);

                    foreach (TouchPoint contact in currentContacts) {
                        aliveContacts.Add(contact);

                        // Put the Contact into the according List
                        if (contact.IsFingerRecognized && (Properties.Settings.Default.SendFingersAsBlobs == false || Properties.Settings.Default.SendFingersAlsoAsBlobs == true)) {
                            aliveFingers.Add(contact);
                        }
                        if (contact.IsFingerRecognized && (Properties.Settings.Default.SendFingersAsBlobs == true || Properties.Settings.Default.SendFingersAlsoAsBlobs == true)) {
                            aliveBlobs.Add(contact);
                        } else if (contact.IsTagRecognized) {
                            aliveTags.Add(contact);
                        } else {
                            aliveBlobs.Add(contact);
                        }
                        TouchPoint c = null;
                        previousContacts.TryGetTouchPointFromId(contact.Id, out c);
                        if (c != null) {
                            if (c.ToString() != contact.ToString()) {
                                changedContacts.Add(contact);

                                // Invoke the processing of a Contact's Manipulation Processor
                                processContactManipulator(contact, currentContacts, previousContacts);

                                // Put the Contact into the according List
                                if (contact.IsFingerRecognized && (Properties.Settings.Default.SendFingersAsBlobs == false || Properties.Settings.Default.SendFingersAlsoAsBlobs == true)) {
                                    changedFingers.Add(contact);
                                }
                                if (contact.IsFingerRecognized && (Properties.Settings.Default.SendFingersAsBlobs == true || Properties.Settings.Default.SendFingersAlsoAsBlobs == true)) {
                                    changedBlobs.Add(contact);
                                } else if (contact.IsTagRecognized) {
                                    changedTags.Add(contact);
                                } else {
                                    changedBlobs.Add(contact);
                                }
                            }
                        } else {
                            addedContacts.Add(contact);
                            // Add a Manipulation Processor to each contact
                            // This is done for extracting the contact velocity directly from Surface SDK
                            addManipulationProcessor(contact);
                            if (contact.IsFingerRecognized &&  (Properties.Settings.Default.SendFingersAsBlobs == false || Properties.Settings.Default.SendFingersAlsoAsBlobs == true)) {
                                addedFingers.Add(contact);
                            }
                            if (contact.IsFingerRecognized && (Properties.Settings.Default.SendFingersAsBlobs == true || Properties.Settings.Default.SendFingersAlsoAsBlobs == true)) {
                                addedBlobs.Add(contact);
                            } else if (contact.IsTagRecognized) {
                                addedTags.Add(contact);
                            } else {
                                addedBlobs.Add(contact);
                            }
                        }
                    }

                    // Send the TUIO messages ////////////////////////////
                    //Console.WriteLine("Sending...");
                    //sendTUIO_2DCur(changedFingers, previousContacts);
                    //sendTUIO_2DObj(changedTags, previousContacts);
                    //sendTUIO_2DBlb(changedBlobs, previousContacts);
                    //////////////////////////////////////////////////////
                    
                    if (addedFingers.Count != 0 || changedFingers.Count != 0 || removedFingers.Count != 0) {
                        sendTUIO_2DCur(aliveFingers, previousContacts);
                    }
                    if (addedTags.Count != 0 || changedTags.Count != 0 || removedTags.Count != 0) {
                        sendTUIO_2DObj(aliveTags, previousContacts);
                    }
                } else {
                    foreach (TouchPoint c in currentContacts) {
                        addedContacts.Add(c);

                        if (c.IsFingerRecognized) {
                            addedFingers.Add(c);
                        }
                        if (c.IsTagRecognized) {
                            addedTags.Add(c);
                        }
                    }
                }
                previousContacts = currentContacts;

                foreach (TouchPoint c in changedContacts) {
                    updateHistoryData(c.Id, c.FrameTimestamp);
                }
                foreach (TouchPoint c in removedContacts) {
                    cleanHistory(c.Id);
                }
            }
            base.Update(gameTime);
        }

        #endregion


        #region Methods for computing Velocities and Accelerations

        /// <summary>
        /// If possible, computes the motion acceleration
        /// This is done by subtracting the preceding Contact velocity from the current contact velocity, devided by the time delta
        /// The unit is acceleration is: Screen Dimesion / Seconds^2
        /// </summary>
        /// <param name="id">Contact ID</param>
        /// <param name="acceleration">Output parameter</param>
        /// <param name="frameTimestamp">Timestamp of the current Contact</param>
        public void getMotionAcceleration(int id, out float acceleration, long frameTimestamp)
        {
            acceleration = 0.0f;

            if (_lastVelocity.ContainsKey(id) && _contactManipulationData.ContainsKey(id))
            {
                float lastVelocityX = _lastVelocity[id].X;
                float lastVelocityY = _lastVelocity[id].Y;

                float currentVelocityX = _contactManipulationData[id].Velocities.LinearVelocityX; // VelocityX;
                float currentVelocityY = _contactManipulationData[id].Velocities.LinearVelocityY; // VelocityY;

                double lastSpeed = Math.Sqrt(Math.Pow((double)lastVelocityX, 2.0) + Math.Pow((double)lastVelocityY, 2.0));
                double currentSpeed = Math.Sqrt(Math.Pow((double)currentVelocityX, 2.0) + Math.Pow((double)currentVelocityY, 2.0));

                long lastTimestamp = _manipulationDataTimestamp[id];
                long currentTimestamp = frameTimestamp;

                long deltaTime = currentTimestamp - lastTimestamp;
                TimeSpan elapsedSpan = new TimeSpan(deltaTime);
                double elapsedSeconds = elapsedSpan.TotalSeconds;
                if (elapsedSeconds > 0.0)
                {
                    acceleration = (float)(currentSpeed - lastSpeed) / (float)elapsedSeconds;
                }

            }
        }

        /// <summary>
        /// Computes the angular velocity and the angular acceleration of a Contact
        /// The unit for the velocity is: Rotations / Seconds
        /// The unit for the acceleration is: Rotations / Seconds^2
        /// </summary>
        /// <param name="currentContact"></param>
        /// <param name="previousContact"></param>
        /// <param name="angularVelocity">Output parameter for the angular velocity</param>
        /// <param name="angularAcceleration">Output parameter for the angular acceleration</param>
        public void computeAngularVelocity(TouchPoint currentContact, TouchPoint previousContact, out float angularVelocity, out float angularAcceleration)
        {
            angularVelocity = 0.0f;
            angularAcceleration = 0.0f;

            float curRot = currentContact.Orientation;
            float lastRot = previousContact.Orientation;
            float deltaRot = curRot - lastRot;

            long deltaTime = currentContact.FrameTimestamp - previousContact.FrameTimestamp;
            TimeSpan elapsedSpan = new TimeSpan(deltaTime);
            double elapsedSeconds = elapsedSpan.TotalSeconds;
            if (elapsedSeconds > 0.0)
            {
                angularVelocity = (float)(deltaRot / (Math.PI * 2)) / (float)elapsedSeconds;

                if (_lastAngularVelocity.ContainsKey(currentContact.Id))
                {
                    angularAcceleration = (angularVelocity - _lastAngularVelocity[currentContact.Id]) / (float)elapsedSeconds;
                    _lastAngularVelocity[currentContact.Id] = angularVelocity;
                    _angularVelocityTimestamp[currentContact.Id] = currentContact.FrameTimestamp;
                }
                else
                {
                    _lastAngularVelocity.Add(currentContact.Id, angularVelocity);
                    _angularVelocityTimestamp.Add(currentContact.Id, currentContact.FrameTimestamp);
                }                    
            }
        }

        /// <summary>
        /// Retrieve the movement velocity of a Contact
        /// The data is taken from a ManipulationProcessor linked to the Contact
        /// The final unit is: SurfaceDimension / Seconds
        /// </summary>
        /// <param name="id">Contact Id</param>
        /// <param name="velocityX">Output parameter</param>
        /// <param name="velocityY">Output parameter</param>
        /// <param name="surface">Object representing the Surface</param>
        public void getVelocity(int id, out float velocityX, out float velocityY) //, InteractiveSurface surface)
        {
            velocityX = 0.0f;
            velocityY = 0.0f;

            if (_contactManipulationData.ContainsKey(id))
            {
                velocityX = _contactManipulationData[id].Velocities.LinearVelocityX; // VelocityX;
                velocityY = _contactManipulationData[id].Velocities.LinearVelocityY; // VelocityY;

                velocityX = velocityX * 1000.0f;
                velocityY = velocityY * 1000.0f;

                velocityX = velocityX / InteractiveSurface.PrimarySurfaceDevice.Width;
                velocityY = velocityY / InteractiveSurface.PrimarySurfaceDevice.Height;
            }
        }

        /// <summary>
        /// Removes all unnecessary entries from the history Dictionaries
        /// </summary>
        /// <param name="id">Contact Id that is no longer active</param>
        public void cleanHistory(int id)
        {
            if (_lastVelocity.ContainsKey(id))
            {
                _lastVelocity.Remove(id);
            }
            if (_manipulationDataTimestamp.ContainsKey(id))
            {
                _manipulationDataTimestamp.Remove(id);
            }
            if (_lastAngularVelocity.ContainsKey(id))
            {
                _lastAngularVelocity.Remove(id);
            }
            if(_angularVelocityTimestamp.ContainsKey(id))
            {
                _lastAngularVelocity.Remove(id);
            }
        }

        /// <summary>
        /// Updates the history data needed for computing Contact velocities
        /// </summary>
        /// <param name="id">Contact Id</param>
        /// <param name="frameTimestamp">Contact Frame Timestamp</param>
        public void updateHistoryData(int id, long frameTimestamp)
        {
            // If there is no ManipulationProcessor handling the current Contact, return
            if (!_contactManipulationData.ContainsKey(id))
                return;

            // Retrieve the velocity values from the ManipulationProcessor and write them into the history Dictionaries
            if (_lastVelocity.ContainsKey(id))
            {
                _lastVelocity[id] = new Vector2(_contactManipulationData[id].Velocities.LinearVelocityX, _contactManipulationData[id].Velocities.LinearVelocityY);
                _manipulationDataTimestamp[id] = frameTimestamp;
            }
            else
            {
                _lastVelocity.Add(id, new Vector2(_contactManipulationData[id].Velocities.LinearVelocityX, _contactManipulationData[id].Velocities.LinearVelocityY));
                _manipulationDataTimestamp.Add(id, frameTimestamp);
            }
        }
        
#endregion

#region Manipulator Methods



        public void processContactManipulator(TouchPoint c, ReadOnlyTouchPointCollection currentContacts, ReadOnlyTouchPointCollection previousContacts)
        {
            List<Manipulator2D> removedManipulators = new List<Manipulator2D>();

            if (previousContacts != null)
            {
                // Find all the contacts that were removed since the last check.
                foreach (TouchPoint contact in previousContacts)
                {
                    // Create a temporary variable for the following method call.
                    TouchPoint tempContact;

                    // Determine if the Contact object from the previous list is in the current list.
                    if (!currentContacts.TryGetTouchPointFromId(contact.Id, out tempContact))
                    {
                        // The contact was not found in the list of current contacts, so it has been removed.

                        // Copy the Contact object information to a new Manipulator object and add 
                        // the Manipulator object to the removedManipulators list.
                        removedManipulators.Add(new Manipulator2D(contact.Id, contact.X, contact.Y));
                    }
                }
            }


            List<Manipulator2D> currentManipulator = new List<Manipulator2D>();
            currentManipulator.Add(new Manipulator2D(c.Id, c.X, c.Y));
            if (_contactProcessors.ContainsKey(c.Id))
            {
                //_contactProcessors[c.Id].ProcessManipulators(currentManipulator, removedManipulators);
                _contactProcessors[c.Id].ProcessManipulators(Timestamp, removedManipulators);
            }
        }

        #region Timestamp
        private long Timestamp
        {
            get
            {
                // Get timestamp in 100-nanosecond units.
                double nanosecondsPerTick = 1000000000.0 / System.Diagnostics.Stopwatch.Frequency;
                return (long)(System.Diagnostics.Stopwatch.GetTimestamp() / nanosecondsPerTick / 100.0);
            }
        }
        #endregion


        /// <summary>
        /// Each Contact gets its own ManipulationProcessor assigned
        /// This way we can use the Manipulator data even if there are multiple Contacts
        /// </summary>
        /// <param name="c">Contact to add the Manipulator to</param>
        public void addManipulationProcessor(TouchPoint c)
        {
            Manipulations2D supportedManipulations =
                  Manipulations2D.TranslateX | Manipulations2D.TranslateY | Manipulations2D.Rotate | Manipulations2D.Scale;

            // Create and initialize a manipulation processor with the supported manipulations.
            ManipulationProcessor2D mp = new ManipulationProcessor2D(supportedManipulations);

            // Add event handlers for manipulation events.
            mp.Started +=
                new EventHandler<Manipulation2DStartedEventArgs>(OnAffine2DManipulationStarted);
            mp.Completed +=
                new EventHandler<Manipulation2DCompletedEventArgs>(OnAffine2DManipulationCompleted);
            mp.Delta +=
                new EventHandler<Manipulation2DDeltaEventArgs>(OnAffine2DDelta);

            _contactProcessors.Add(c.Id, mp);
        }

        /// <summary>
        /// Removes ManipulationProcessor assigned to removed Contacts
        /// </summary>
        /// <param name="removedContacts"></param>
        public void cleanManipulationProcessorList(List<TouchPoint> removedContacts)
        {
            foreach (TouchPoint c in removedContacts)
            {
                if( _contactProcessors.ContainsKey( c.Id ) )
                {
                    _contactProcessors.Remove(c.Id);
                }
            }
        }
        #endregion

#region TUIO stuff
        /// <summary>
        /// Sends a /tuio/2Dobj message
        /// The remote Host is specified in the appConfig
        /// The message is built according to TUIO 1.1 specifications (see http://www.tuio.org/?specification)
        /// </summary>
        /// <param name="contacts">Current acitve Contacts</param>
        /// <param name="previousContacts">Contacts from the previous frame</param>
        public void sendTUIO_2DObj(List<TouchPoint> contacts, ReadOnlyTouchPointCollection previousContacts)
        {
            //if (contacts.Count == 0) return;
//#warning Properties about the device are now on the InteractiveSurfaceDevice class.
            //InteractiveSurface surface = Microsoft.Surface.Core.InteractiveSurfaceDevice.PrimarySurfaceDevice;
            //double width = InteractiveSurface.PrimarySurfaceDevice.Right - InteractiveSurface.PrimarySurfaceDevice.Left;
            //double height = InteractiveSurface.PrimarySurfaceDevice.Bottom - InteractiveSurface.PrimarySurfaceDevice.Top;
            InteractiveSurfaceDevice surface = InteractiveSurface.PrimarySurfaceDevice;
            double width = surface.Right - surface.Left;
            double height = surface.Bottom - surface.Top;

            OSCBundle objectBundle = new OSCBundle();
            OSCMessage sourceMessage = TUIO_2DObj.sourceMessage();
            objectBundle.Append(sourceMessage);
            
            OSCMessage aliveMessage = TUIO_2DObj.aliveMessage(contacts);
            objectBundle.Append(aliveMessage);

            for (int i = 0; i < contacts.Count; i++)
            {
                TouchPoint c = contacts[i];
                double x = (c.CenterX - surface.Left) / width;
                double y = (c.CenterY - surface.Top) / height;

                float angularVelocity = 0.0f;
                float angularAcceleration = 0.0f;

                if( previousContacts.Contains( c.Id ))
                {
                    computeAngularVelocity(c, previousContacts.GetTouchPointFromId(c.Id), out angularVelocity, out angularAcceleration);
                }
                
                float X = 0.0f;
                float Y = 0.0f;
                getVelocity(c.Id, out X, out Y); //, InteractiveSurface.PrimarySurfaceDevice.);
                float motionAcceleration = 0.0f;
                getMotionAcceleration(c.Id, out motionAcceleration, c.FrameTimestamp);

                //if (c.Tag.Value)
                {
                    OSCMessage setMessage = TUIO_2DObj.setMessage(c.Id, (int)c.Tag.Value, (float)x, (float)y, (float)c.Orientation, X, Y, angularVelocity, motionAcceleration, angularAcceleration);
                    objectBundle.Append(setMessage);
                }
                /*else if (c.Tag.Series)
                {
                    OSCMessage setMessage = TUIO_2DObj.setMessage(c.Id, (int)c.Tag.Series, (float)x, (float)y, (float)c.Orientation, X, Y, angularVelocity, motionAcceleration, angularAcceleration);
                    objectBundle.Append(setMessage);
                }*/

            }
            OSCMessage frameMessage = TUIO_2DObj.frameMessage(_Frame);
            objectBundle.Append(frameMessage);
            _Frame++;
            _OSCSender.Send(objectBundle);
        }

        /// <summary>
        /// Sends a /tuio/2Dcur message
        /// The remote Host is specified in the appConfig
        /// The message is built according to TUIO 1.1 specifications (see http://www.tuio.org/?specification)
        /// </summary>
        /// <param name="contacts">Current active contacts</param>
        /// <param name="previousContacts">Contacts from the previous frame</param>
        public void sendTUIO_2DCur(List<TouchPoint> contacts, ReadOnlyTouchPointCollection previousContacts)
        {
            //if (contacts.Count == 0) return;
//#warning Properties about the device are now on the InteractiveSurfaceDevice class.
            //InteractiveSurface surface = Microsoft.Surface.Core.InteractiveSurface.PrimarySurfaceDevice;
            //double width = InteractiveSurface.PrimarySurfaceDevice.Right - InteractiveSurface.PrimarySurfaceDevice.Left;
            //double height = InteractiveSurface.PrimarySurfaceDevice.Bottom - InteractiveSurface.PrimarySurfaceDevice.Top;
            InteractiveSurfaceDevice surface = InteractiveSurface.PrimarySurfaceDevice;
            double width = surface.Right - surface.Left;
            double height = surface.Bottom - surface.Top;

            OSCBundle cursorBundle = new OSCBundle();
            OSCMessage sourceMessage = TUIO_2DCur.sourceMessage();
            cursorBundle.Append(sourceMessage);
            
            OSCMessage aliveMessage = TUIO_2DCur.aliveMessage(contacts);
            cursorBundle.Append(aliveMessage);

            for (int i = 0; i < contacts.Count; i++)
            {
                TouchPoint c = contacts[i];

                double x = (c.CenterX - surface.Left) / width;
                double y = (c.CenterY - surface.Top) / height;

                float X = 0.0f;
                float Y = 0.0f;
                getVelocity(c.Id, out X, out Y); //, surface);
                float motionAcceleration = 0.0f;
                getMotionAcceleration(c.Id, out motionAcceleration, c.FrameTimestamp);

                OSCMessage setMessage = TUIO_2DCur.setMessage(c.Id, (float)x, (float)y, X, Y, motionAcceleration);
                cursorBundle.Append(setMessage);
            }
            OSCMessage frameMessage = TUIO_2DCur.frameMessage(_Frame);
            cursorBundle.Append(frameMessage);
            _Frame++;
            _OSCSender.Send(cursorBundle);
        }

        /// <summary>
        /// Sends a /tuio/2Dblb message
        /// The remote Host is specified in the appConfig
        /// The message is built according to TUIO 1.1 specifications (see http://www.tuio.org/?specification)
        /// </summary>
        /// <param name="contacts">Current active contacts</param>
        /// <param name="previousContacts">Contacts from the previous frame</param>
        public void sendTUIO_2DBlb(List<TouchPoint> contacts, ReadOnlyTouchPointCollection previousContacts)
        {
            //if (contacts.Count == 0) return;
//#warning Properties about the device are now on the InteractiveSurfaceDevice class.
            //InteractiveSurface surface = Microsoft.Surface.Core.InteractiveSurface.PrimarySurfaceDevice;
            //double width = InteractiveSurface.PrimarySurfaceDevice.Right - InteractiveSurface.PrimarySurfaceDevice.Left;
            //double height = InteractiveSurface.PrimarySurfaceDevice.Bottom - InteractiveSurface.PrimarySurfaceDevice.Top;
            InteractiveSurfaceDevice surface = InteractiveSurface.PrimarySurfaceDevice;
            double width = surface.Right - surface.Left;
            double height = surface.Bottom - surface.Top;

            OSCBundle blobBundle = new OSCBundle();
            OSCMessage sourceMessage = TUIO_2DBlb.sourceMessage();
            blobBundle.Append(sourceMessage);

            OSCMessage aliveMessage = TUIO_2DBlb.aliveMessage(contacts);
            blobBundle.Append(aliveMessage);

            for (int i = 0; i < contacts.Count; i++)
            {
                TouchPoint c = contacts[i];
                double x = (c.CenterX - surface.Left) / width;
                double y = (c.CenterY - surface.Top) / height;
                double w = c.MajorAxis / surface.Width;
                double h = c.MinorAxis / surface.Height;
                double f = c.PhysicalArea / (surface.Width * surface.Height);

                float angularVelocity = 0.0f;
                float angularAcceleration = 0.0f;

                if (previousContacts.Contains(c.Id))
                {
                    computeAngularVelocity(c, previousContacts.GetTouchPointFromId(c.Id), out angularVelocity, out angularAcceleration);
                }

                float X = 0.0f;
                float Y = 0.0f;
                getVelocity(c.Id, out X, out Y); //, surface);
                
                float motionAcceleration = 0.0f;
                getMotionAcceleration(c.Id, out motionAcceleration, c.FrameTimestamp);

                OSCMessage setMessage = TUIO_2DBlb.setMessage(c.Id, (float)x, (float)y, (float)c.Orientation, (float)w, (float)h, (float)(w * h), X, Y, angularVelocity, motionAcceleration, angularAcceleration);
                blobBundle.Append(setMessage);
            }
            OSCMessage frameMessage = TUIO_2DBlb.frameMessage(_Frame);
            blobBundle.Append(frameMessage);
            _Frame++;
            _OSCSender.Send(blobBundle);
        }

#endregion

#region Default Window Stuff

        /// <summary>
        /// This is called when the app should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            if (!applicationLoadCompleteSignalled)
            {
                // Dismiss the loading screen now that we are starting to draw
                ApplicationServices.SignalApplicationLoadComplete();
                applicationLoadCompleteSignalled = true;
            }

            //TODO: Rotate the UI based on the value of screenTransform here if desired

            //graphics.GraphicsDevice.Clear(backgroundColor);

            //TODO: Add your drawing code here
            //TODO: Avoid any expensive logic if application is neither active nor previewed

            base.Draw(gameTime);
        }

        /// <summary>
        /// This is called when application has been activated.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnApplicationActivated(object sender, EventArgs e)
        {
            // update application state
            isApplicationActivated = true;
            isApplicationPreviewed = false;

            //TODO: Enable audio, animations here

            //TODO: Optionally enable raw image here
        }

        /// <summary>
        /// This is called when application is in preview mode.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnApplicationPreviewed(object sender, EventArgs e)
        {
            // update application state
            isApplicationActivated = false;
            isApplicationPreviewed = true;

            //TODO: Disable audio here if it is enabled

            //TODO: Optionally enable animations here
        }

        /// <summary>
        ///  This is called when application has been deactivated.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnApplicationDeactivated(object sender, EventArgs e)
        {
            // update application state
            isApplicationActivated = false;
            isApplicationPreviewed = false;

            //TODO: Disable audio, animations here

            //TODO: Disable raw image if it's enabled
        }

        #endregion
    }
}
