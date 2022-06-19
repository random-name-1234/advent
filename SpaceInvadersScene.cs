using System;
using System.Collections.Generic;
using System.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace advent
{
    public class SpaceInvadersScene : ISpecialScene
    {
        private const int PlayerTop = 29;
        private const double PlayerSpeed = 15;
        public bool IsActive { get; private set; }

        public bool HidesTime { get; private set; } = true;

        public bool RainbowSnow => false;
        public string Name => "Space Invaders";

        private TimeSpan elapsedThisScene;
        private Image<Rgba32> player;
        private Image<Rgba32> invader1;
        private Image<Rgba32> exploding1;
        private Image<Rgba32> blocksTemplate;
        private Random random;

        private bool[,] blocks;

        private int playerX;
        private double playerActualX;
        private static TimeSpan sceneDuration = TimeSpan.FromSeconds(60);
        private static TimeSpan explodeDuration = TimeSpan.FromSeconds(0.5);
        private TimeSpan timePerInvaderStep;
        private static readonly List<Point> invaderPositionSequence;
        private static readonly int maxInvaderPositionIndex;
        private int invaderPositionIndex;
        private List<Invader> invaders;
        private List<Missile> missiles;
        private TimeSpan timeUntilInvaderStep;
        private bool isPlayerAlive;
        private TimeSpan playerExplodeTimeLeft;
        private int playerTargetX;
        private int playerLastTarget;
        private TimeSpan playerWaitTimeLeft;
        private bool isPlayerWaiting;
        //private bool isPlayerAvoiding;
        private TimeSpan playerTimeToFire;

        static SpaceInvadersScene()
        {
            invaderPositionSequence = new List<Point>();
            int y = 0;
            bool goingRight = true;
            while (y < 10)
            {
                for (int x = 0; x < 17; x++)
                {
                    if (goingRight)
                    {
                        invaderPositionSequence.Add(new Point(x, y));
                    }
                    else
                    {
                        invaderPositionSequence.Add(new Point(16 - x, y));
                    }
                }

                goingRight = !goingRight;
                y++;
            }

            maxInvaderPositionIndex = invaderPositionSequence.Count - 2;
        }

        public SpaceInvadersScene()
        {
            random = new Random();
            blocks = new bool[64, 32];

            IsActive = false;
            player = Image.Load<Rgba32>("space-invaders-player.png");
            invader1 = Image.Load<Rgba32>("space-invaders-invader1.png");
            exploding1 = Image.Load<Rgba32>("space-invaders-exploding1.png");
            blocksTemplate = Image.Load<Rgba32>("space-invaders-blocks.png");
            invaders = new List<Invader>();
            missiles = new List<Missile>();

            ResetScene();
        }

        public void Activate()
        {
            elapsedThisScene = TimeSpan.Zero;
            IsActive = true;
            ResetScene();
        }

        private void ResetScene()
        {
            isPlayerAlive = true;
            playerX = 35;
            playerActualX = 35.0;
            timePerInvaderStep = TimeSpan.FromSeconds(0.6);
            timeUntilInvaderStep = timePerInvaderStep;
            invaderPositionIndex = 0;
            invaders.Clear();
            missiles.Clear();
            playerTargetX = 10;
            playerLastTarget= 31;
            playerWaitTimeLeft = TimeSpan.FromSeconds(random.NextDouble() * 2.0);
            isPlayerWaiting = true;
            //isPlayerAvoiding = false;
            playerTimeToFire = TimeSpan.FromSeconds(0.5);

            // Reset blocks
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    blocks[x, y] = (blocksTemplate[x, y].A != 0);
                }
            }

            // Create invaders
            for (int i = 0; i < 46; i += 10)
            {
                invaders.Add(new Invader(new Point(i, 0)));
                invaders.Add(new Invader(new Point(i, 6)));
            }
        }

        public void Elapsed(TimeSpan timeSpan)
        {
            elapsedThisScene += timeSpan;
            if (elapsedThisScene > sceneDuration)
            {
                IsActive = false;
            }

            if (!isPlayerAlive)
            {
                playerExplodeTimeLeft -= timeSpan;

                if (playerExplodeTimeLeft < TimeSpan.Zero)
                {
                    IsActive = false;
                }
            }
            else
            {
                if (isPlayerWaiting)
                {
                    playerWaitTimeLeft -= timeSpan;
                    if (playerWaitTimeLeft < TimeSpan.Zero)
                    {
                        isPlayerWaiting = false;
                        SelectNextPlayerTarget();
                    }
                }

                if (!isPlayerWaiting)
                {
                    double direction = playerTargetX < playerX ? -1 : 1;
                    //int searchOffset = (int)direction * 4;
                    //int searchX1 = Math.Max(0, playerX - 3 + searchOffset)

                    if (playerX != playerTargetX)
                    {
                        playerActualX += direction * timeSpan.TotalSeconds * PlayerSpeed;
                        playerX = (int)playerActualX;
                    }
                    else
                    {
                        isPlayerWaiting = true;
                        playerWaitTimeLeft = TimeSpan.FromSeconds(random.NextDouble() * 1.0);
                    }
                }

                playerTimeToFire -= timeSpan;
                if (playerTimeToFire < TimeSpan.Zero)
                {
                    playerTimeToFire += TimeSpan.FromSeconds(0.5);
                    double chance = 1;
                    for (int y = 0; y < 31; y++)
                    {
                        if (blocks[playerX, y])
                        {
                            chance = random.NextDouble();
                            break;
                        }
                    }

                    if (chance > 0.8)
                    {
                        missiles.Add(new Missile(new Point(playerX, PlayerTop), true));
                    }
                }
            }

            // Invaders
            timeUntilInvaderStep -= timeSpan;
            if (timeUntilInvaderStep < TimeSpan.Zero)
            {
                timeUntilInvaderStep += timePerInvaderStep;
                if (invaderPositionIndex < maxInvaderPositionIndex)
                {
                    invaderPositionIndex++;
                    Point invaderDelta = invaderPositionSequence[invaderPositionIndex];
                    timePerInvaderStep = TimeSpan.FromSeconds(0.6 - (0.05 * invaderDelta.Y));
                    foreach (var invader in invaders.Where(i => i.IsAlive))
                    {
                        invader.Offset = invaderDelta;
                    }
                }
            }

            foreach (var invader in invaders)
            {
                invader.Elapsed(timeSpan);
                if (invader.ReadyToFire)
                {
                    invader.ReadyToFire = false;
                    missiles.Add(new Missile(new Point(invader.Position.X + 3, invader.Position.Y + 3), false));
                }
            }

            // Missiles
            foreach (var missile in missiles)
            {
                missile.Elapsed(timeSpan, playerX, invaders, ref blocks, out bool isPlayerHit);
                if (isPlayerHit && isPlayerAlive)
                {
                    playerExplodeTimeLeft = explodeDuration;
                    isPlayerAlive = false;
                }
            }

            missiles.RemoveAll(x => !x.IsActive);
            invaders.RemoveAll(x => x.CanBeRemoved);
            if (invaders.Count == 0)
            {
                IsActive = false;
            }
        }

        private void SelectNextPlayerTarget()
        {
            playerLastTarget = playerTargetX;
            while (playerTargetX == playerLastTarget)
            {
                switch (random.Next(0, 3))
                {
                    case 0:
                        playerTargetX = 14;
                        break;
                    case 1:
                        playerTargetX = 35;
                        break;
                    case 2:
                        playerTargetX = 56;
                        break;
                    default:
                        playerTargetX = random.Next(3, 60);
                        break;
                }
            }
        }

        public void Draw(Image<Rgba32> img)
        {
            if (IsActive)
            {
                foreach (var invader in invaders)
                {
                    if (invader.IsAlive)
                    {
                        img.Mutate(x => x.DrawImage(invader1, invader.Position, 1f));
                    }
                    else
                    {
                        img.Mutate(x => x.DrawImage(exploding1, invader.Position, 1f));
                    }
                }

                foreach (var missile in missiles)
                {
                    Rgba32 colour = missile.IsPlayer ? Color.LightGreen : Color.Red;
                    img[missile.X, missile.Y - 1] = colour;
                    img[missile.X, missile.Y] = colour;
                    img[missile.X, missile.Y + 1] = colour;
                }

                // Draw blocks
                for (int y = 0; y < 32; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        if (blocks[x, y])
                        {
                            img[x, y] = Color.DarkSeaGreen;
                        }
                    }
                }

                // Draw player
                if (isPlayerAlive)
                {
                    img.Mutate(x => x.DrawImage(player, new Point(playerX - 3, PlayerTop), 1f));
                }
                else
                {
                    if (playerExplodeTimeLeft > TimeSpan.Zero)
                    {
                        img.Mutate(x => x.DrawImage(exploding1, new Point(playerX - 3, PlayerTop - 1), 1f));
                    }
                }
            }
        }

        private class Invader
        {
            TimeSpan timeToFire;
            private readonly Random random;
            private readonly Point startPosition;
            private static TimeSpan explodeDuration = TimeSpan.FromSeconds(0.5);
            private TimeSpan explodeTimeLeft;

            public bool IsAlive { get; set; }
            public bool CanBeRemoved { get; private set; }

            public Point Position => new Point(startPosition.X + Offset.X, startPosition.Y + Offset.Y);

            public bool ReadyToFire { get; set; }
            public Point Offset { get; set; }

            public Invader(Point startPosition)
            {
                CanBeRemoved = false;
                random = new Random();
                IsAlive = true;
                this.startPosition = startPosition;
                Offset = new Point(0, 0);
                ReadyToFire = false;
                ResetTimeToFire();
            }

            private void ResetTimeToFire()
            {
                timeToFire = TimeSpan.FromSeconds(5.0 + 5.0 * random.NextDouble());
            }

            public void Elapsed(TimeSpan timeSpan)
            {
                if (IsAlive)
                {
                    timeToFire -= timeSpan;
                    if (timeToFire < TimeSpan.Zero)
                    {
                        ReadyToFire = true;
                        ResetTimeToFire();
                    }
                }
                else
                {
                    explodeTimeLeft -= timeSpan;
                    if (explodeTimeLeft < TimeSpan.Zero)
                    {
                        CanBeRemoved = true;
                    }
                }
            }

            internal void Hit()
            {
                explodeTimeLeft = explodeDuration;
                IsAlive = false;
            }
        }

        private class Missile
        {
            private double speed;
            private double currentY;
            public bool IsActive { get; set; }

            public bool IsPlayer { get; set; }

            public int X { get; }
            public int Y => (int)currentY;

            public Missile(Point startPosition, bool isPlayer)
            {
                X = startPosition.X;
                currentY = startPosition.Y;
                IsPlayer = isPlayer;
                speed = isPlayer ? -30 : 15;
                IsActive = true;
            }

            internal void Elapsed(TimeSpan timeSpan, int playerX, List<Invader> invaders, ref bool[,] blocks, out bool playerHit)
            {
                playerHit = false;
                bool canBreak = false;
                int lastY = Y;
                currentY += timeSpan.TotalSeconds * speed;

                if (Y < 1 || Y > 30)
                {
                    IsActive = false;
                }
                else
                {
                    foreach (int pathY in GetPath(lastY, Y))
                    {
                        if (blocks[X, pathY])
                        {
                            blocks[X, pathY] = false;
                            IsActive = false;
                            canBreak = true;
                            break;
                        }

                        if (IsPlayer && IsActive)
                        {
                            foreach (var invader in invaders)
                            {
                                if (IsWithinRect(X, pathY, invader.Position.X, invader.Position.Y, invader.Position.X + 7, invader.Position.Y + 5))
                                {
                                    invader.Hit();
                                    IsActive = false;
                                    canBreak = true;
                                    break;
                                }
                            }
                        }
                        else if (IsActive)
                        {
                            if (IsWithinRect(X, pathY, playerX - 3, PlayerTop, playerX + 3, 31))
                            {
                                playerHit = true;
                                canBreak = true;
                            }
                        }
                        if (canBreak) break;
                    }
                }
            }

            private bool IsWithinRect(int missileX, int missileY, int x1, int y1, int x2, int y2)
            {
                return missileX >= x1 && missileX <= x2 && missileY >= y1 & missileY <= y2;
            }

            private IEnumerable<int> GetPath(int oldY, int newY)
            {
                if (oldY == newY)
                {
                    yield return newY;
                }
                else
                {
                    int direction = oldY > newY ? -1 : 1;
                    for (int i = 1; i <= Math.Abs(oldY - newY); i++)
                    {
                        yield return oldY + i * direction;
                    }
                }
            }
        }
    }
}
