using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using MineSweeper.Properties;

namespace MineSweeper
{
    public partial class Game : Form
    {
        private int HeightTiles = 20;
        private int WidthTiles = 20;
        private int NumOfBombs = 10;
        private int[,] Grid;
        private States[,] StateGrid;

        private List<int> BombList;
        private bool Started;
        private BackgroundWorker bkWkr;

        private enum States
        {
            Revealed,
            Questioned,
            Flagged,
            Covered
        }

        public Game()
        {
            InitializeComponent();
            GenerateNewGame();
        }

        private void ClearTiles()
        {
            bool AllGone = false;

            while (!AllGone)
            {
                AllGone = true;

                for (int i = 0; i < this.Controls.Count; i++)
                {
                    if (this.Controls[i] is Button)
                    {
                        AllGone = false;
                        this.Controls[i].Dispose();
                    }
                }
            }

            AllGone = false;

            while (!AllGone)
            {
                AllGone = true;

                for (int i = 0; i < this.Controls.Count; i++)
                {
                    if (this.Controls[i] is PictureBox)
                    {
                        PictureBox picture = this.Controls[i] as PictureBox;
                        if (picture != null)
                        {
                            AllGone = false;
                            this.Controls.Remove(picture);
                            picture.Image.Dispose();
                            picture.ImageLocation = null;
                            picture.Dispose();
                        }
                    }
                }
            }            
        }

        private void GenerateNewGame()
        {
            ClearTiles();

            BombList = new List<int>();
            Grid = new int[WidthTiles + 1, HeightTiles + 1];
            StateGrid = new States[WidthTiles + 1, HeightTiles + 1];
            Started = false;
            bkWkr = new BackgroundWorker();

            this.lbl_BombCount.Text = NumOfBombs.ToString();
            this.lbl_Timer.Text = "0";

            // First row not used for simplicity
            for (int i = 1; i <= HeightTiles; i++)
            {
                for (int j = 1; j <= WidthTiles; j++)
                {
                    Grid[i, j] = 0;
                    StateGrid[i, j] = States.Covered;
                }
            }

            this.Size = new Size((WidthTiles * 40) + 32 + (WidthTiles * 2), (HeightTiles * 40) + 125 + (HeightTiles * 2));
            this.lbl_BombCount.Location = new Point(12, (HeightTiles * 40) + 50 + (HeightTiles * 2));
            this.lbl_Timer.Location = new Point(this.Width - 50, (HeightTiles * 40) + 50 + (HeightTiles * 2));

            GenerateTilesAndCover();

            // Distribute bombs
            DistributeBombs();

            // Place numbers
            DistrubuteNumbers();
        }

        private void DidUserWin()
        {
            int count = 0;

            if (int.Parse(this.lbl_BombCount.Text) == 0)
            {
                for (int i = 1; i <= HeightTiles; i++)
                {
                    for (int j = 1; j <= WidthTiles; j++)
                    {
                        //Count the number of covered tiles
                        if (StateGrid[i, j] == States.Covered || StateGrid[i, j] == States.Questioned)
                            count++;
                    }
                }

                if (count == 0)
                {
                    bkWkr.CancelAsync();
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("You won!!!");
                    sb.AppendLine(String.Format("The time it took you: {0} seconds", this.lbl_Timer.Text));
                    MessageBox.Show(sb.ToString(), "YOU WON!!");
                }
            }
        }

        private void button_Click(object sender, MouseEventArgs e)
        {
            Button button = sender as Button;
            MatchCollection coordinates = Regex.Matches(button.Name, "\\d+");
            int y = int.Parse(coordinates[0].Value);
            int x = int.Parse(coordinates[1].Value);

            if (!Started)
            {
                Started = true;

                // Start timer
                bkWkr.DoWork += new DoWorkEventHandler(bkWkr_DoWork);
                bkWkr.WorkerSupportsCancellation = true;
                bkWkr.RunWorkerAsync();
            }

            // Determine what image the button has (nothing, flag or question)
            if (StateGrid[y, x] == States.Covered)
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                {
                    button.Visible = false;
                    RevealTile(y, x);

                    // Determine if click was a bomb!
                    if (coordinates.Count == 2)
                    {
                        if (Grid[int.Parse(coordinates[0].Value), int.Parse(coordinates[1].Value)] == -1)
                        {
                            RevealBombsAndDisableButtons();
                            bkWkr.CancelAsync();
                            return;
                        }
                    }

                    ClearSurroundingBlanks(y, x);
                }
                else if (e.Button == System.Windows.Forms.MouseButtons.Right)
                {
                    button.Image = Resources.flag;
                    StateGrid[y, x] = States.Flagged;
                    CountFlags();
                }
            }
            else if (StateGrid[y, x] == States.Flagged)
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Right)
                {
                    button.Image = Resources.question;
                    StateGrid[y, x] = States.Questioned;
                    CountFlags();
                }
            }
            else if (StateGrid[y, x] == States.Questioned)
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Right)
                {
                    button.Image = null;
                    StateGrid[y, x] = States.Covered;
                }
            }

            DidUserWin();
        }

        void bkWkr_DoWork(object sender, DoWorkEventArgs e)
        {
            while (true)
            {
                BackgroundWorker bk = sender as BackgroundWorker;

                if (lbl_Timer.Text.Equals("999") || bk.CancellationPending)
                {
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                    if (this.lbl_Timer.InvokeRequired && !bk.CancellationPending)
                    {
                        lbl_Timer.Invoke(new MethodInvoker(delegate { lbl_Timer.Text = (int.Parse(lbl_Timer.Text) + 1).ToString(); }));
                    }
                }
            }
        }

        private void ClearSurroundingBlanks(int y, int x)
        {
            // If tile is a number, return
            if (Grid[y, x] > 0)
            {
                return;
            }

            /**************************************************/
            // Right Top, Right Middle, Right Bottom
            /**************************************************/
            if (x + 1 <= WidthTiles && StateGrid[y, x + 1] == States.Covered)
            {
                RevealTile(y, x + 1);
                ClearSurroundingBlanks(y, x + 1);
            }

            if (x + 1 <= WidthTiles && y + 1 <= HeightTiles && StateGrid[y + 1, x + 1] == States.Covered)
            {
                RevealTile(y + 1, x + 1);
                ClearSurroundingBlanks(y + 1, x + 1);
            }

            if (x + 1 <= WidthTiles && y - 1 >= 1 && StateGrid[y - 1, x + 1] == States.Covered)
            {
                RevealTile(y - 1, x + 1);
                ClearSurroundingBlanks(y - 1, x + 1);
            }

            /**************************************************/
            // Left Top, Left Middle, Left Bottom
            /**************************************************/
            if (x - 1 >= 1 && StateGrid[y, x - 1] == States.Covered)
            {
                RevealTile(y, x - 1);
                ClearSurroundingBlanks(y, x - 1);
            }

            if (x - 1 >= 1 && y + 1 <= HeightTiles && StateGrid[y + 1, x - 1] == States.Covered)
            {
                RevealTile(y + 1, x - 1);
                ClearSurroundingBlanks(y + 1, x - 1);
            }

            if (x - 1 >= 1 && y - 1 >= 1 && StateGrid[y - 1, x - 1] == States.Covered)
            {
                RevealTile(y - 1, x - 1);
                ClearSurroundingBlanks(y - 1, x - 1);
            }

            /**************************************************/
            // Middle Top, Middle Bottom
            /**************************************************/
            if (y + 1 <= HeightTiles && StateGrid[y + 1, x] == States.Covered)
            {
                RevealTile(y + 1, x);
                ClearSurroundingBlanks(y + 1, x);
            }

            if (y - 1 >= 1 && StateGrid[y - 1, x] == States.Covered)
            {
                RevealTile(y - 1, x);
                ClearSurroundingBlanks(y - 1, x);
            }
        }

        private void RevealTile(int y, int x)
        {
            string tileName = string.Concat("tile_", y.ToString(), "_", x.ToString());
            StateGrid[y, x] = States.Revealed;

            foreach (Control control in this.Controls)
            {
                if (control.Name == tileName)
                {
                    (control as PictureBox).Visible = true;
                    break;
                }
            }
        }

        private void CountFlags()
        {
            int count = 0;
            for (int y = 1; y <= HeightTiles; y++)
            {
                for (int x = 1; x <= WidthTiles; x++)
                {
                    if (StateGrid[y, x] == States.Flagged)
                    {
                        count++;
                    }
                }
            }
            this.lbl_BombCount.Text = (NumOfBombs - count).ToString();
        }

        private void RevealBombsAndDisableButtons()
        {
            for (int y = 1; y <= HeightTiles; y++)
            {
                for (int x = 1; x <= WidthTiles; x++)
                {
                    if (Grid[y, x] == -1)
                    {
                        // Hide button to reveal bomb
                        RevealBomb(y, x);
                    }
                    Button button = GetButton(string.Concat("button_", y.ToString(), "_", x.ToString()));
                    button.MouseUp -= new MouseEventHandler(this.button_Click);
                }
            }
        }

        private Button GetButton(string name)
        {
            foreach (Control control in this.Controls)
            {
                if (control.Name.Equals(name))
                    return control as Button;
            }
            return new Button();
        }

        private void RevealBomb(int y, int x)
        {
            foreach (Control control in this.Controls)
            {
                if (control is Button)
                {
                    (control as Button).MouseClick -= new MouseEventHandler(this.button_Click);
                    if (control.Name.Equals(string.Concat("button_", y.ToString(), "_", x.ToString())))
                        (control as Button).Visible = false;
                }
                if (control is PictureBox && control.Name.Equals(string.Concat("tile_", y.ToString(), "_", x.ToString())))
                {
                    (control as PictureBox).Visible = true;
                    (control as PictureBox).Image = Resources.bombfound;
                }
            }
        }

        private void CreateButton(int y, int x)
        {
            Button button = new Button();
            button.Size = new Size(40, 40);
            // 12 = Initial location
            // ((x - 1) * 40) = Number of boxes * width/height of box
            // ((x - 1) * 2) = Number of boxes * padding(2)
            button.Location = new Point(12 + ((x - 1) * button.Width) + ((x - 1) * 2), 36 + ((y - 1) * button.Height) + ((y - 1) * 2));
            button.Name = string.Concat("button_", y, "_", x);
            button.MouseUp -= new MouseEventHandler(this.button_Click);
            button.MouseUp += new MouseEventHandler(this.button_Click);
            
            button.Visible = true;

            this.Controls.Add(button);
        }

        private void GenerateTilesAndCover()
        {
            for (int y = 1; y <= HeightTiles; y++)
            {
                for (int x = 1; x <= WidthTiles; x++)
                {
                    GenerateTile(y, x);
                    CreateButton(y, x);
                }
            }
        }

        private void GenerateTile(int y, int x)
        {
            // x and y are the box x and y number, not coordinates

            PictureBox box = new PictureBox();
            box.Image = Resources.blank;
            box.Size = new Size(40, 40);
            // 12 = Initial location
            // ((x - 1) * 40) = Number of boxes * width/height of box
            // ((x - 1) * 2) = Number of boxes * padding(2)
            box.Location = new Point(12 + ((x - 1) * box.Width) + ((x - 1) * 2), 36 + ((y - 1) * box.Height) + ((y - 1) * 2));
            box.Name = string.Concat("tile_", y, "_", x);
            box.Visible = false;

            this.Controls.Add(box);
        }

        private void DistrubuteNumbers()
        {
            int count = -1;

            for (int y = 1; y <= HeightTiles; y++)
            {
                for (int x = 1; x <= WidthTiles; x++)
                {
                    if (Grid[y, x] == -1)
                        continue;

                    count = CountSurroundingBombs(y, x);

                    switch (count)
                    {
                        case 1:
                            SetTileImage(string.Concat("tile_", y.ToString(), "_", x.ToString()), Resources.one);
                            break;
                        case 2:
                            SetTileImage(string.Concat("tile_", y.ToString(), "_", x.ToString()), Resources.two);
                            break;
                        case 3:
                            SetTileImage(string.Concat("tile_", y.ToString(), "_", x.ToString()), Resources.three);
                            break;
                        case 4:
                            SetTileImage(string.Concat("tile_", y.ToString(), "_", x.ToString()), Resources.four);
                            break;
                        case 5:
                            SetTileImage(string.Concat("tile_", y.ToString(), "_", x.ToString()), Resources.five);
                            break;
                        case 6:
                            SetTileImage(string.Concat("tile_", y.ToString(), "_", x.ToString()), Resources.six);
                            break;
                        case 7:
                            SetTileImage(string.Concat("tile_", y.ToString(), "_", x.ToString()), Resources.seven);
                            break;
                        case 8:
                            SetTileImage(string.Concat("tile_", y.ToString(), "_", x.ToString()), Resources.eight);
                            break;
                        default:
                            SetTileImage(string.Concat("tile_", y.ToString(), "_", x.ToString()), Resources.blank);
                            break;
                    }
                }
            }
        }

        private int CountSurroundingBombs(int y, int x)
        {
            int tally = 0;

            // Counterclockwise from right
            // dx = 1,  dy = 0 - Center Right
            if (x + 1 <= WidthTiles)
            {
                if (Grid[y, x + 1] == -1)
                    tally++;
            }

            // dx = 1,  dy = -1 - Top Right
            if (x + 1 <= WidthTiles && y - 1 >= 1)
            {
                if (Grid[y - 1, x + 1] == -1)
                    tally++;
            }

            // dx = 0,  dy = -1 - Top Center
            if (y - 1 >= 1)
            {
                if (Grid[y - 1, x] == -1)
                    tally++;
            }

            // dx = -1, dy = -1 - Top Left
            if (x - 1 >= 1 && y - 1 >= 1)
            {
                if (Grid[y - 1, x - 1] == -1)
                    tally++;
            }

            // dx = -1, dy = 0 - Left Center
            if (x - 1 >= 1)
            {
                if (Grid[y, x - 1] == -1)
                    tally++;
            }

            // dx = -1, dy = 1 - Bottom Left
            if (x - 1 >= 1 && y + 1 <= HeightTiles)
            {
                if (Grid[y + 1, x - 1] == -1)
                    tally++;
            }

            // dx = 0,  dy = 1 - Bottom Center
            if (y + 1 <= HeightTiles)
            {
                if (Grid[y + 1, x] == -1)
                    tally++;
            }

            // dx = 1,  dy = 1 - Bottom Right
            if (x + 1 <= WidthTiles && y + 1 <= HeightTiles)
            {
                if (Grid[y + 1, x + 1] == -1)
                    tally++;
            }

            Grid[y, x] = tally;

            return tally;
        }

        private void SetTileImage(string name, Bitmap image)
        {
            foreach (Control ctrl in this.Controls)
            {
                if (ctrl is PictureBox)
                {
                    if (ctrl.Name == name)
                    {
                        (ctrl as PictureBox).Image = image;
                        break;
                    }
                }
            }
        }

        private void DistributeBombs()
        {
            int count = NumOfBombs;
            Random random = new Random();
            int index = 0;

            while (count > 0)
            {
                index = random.Next(1, HeightTiles * WidthTiles);
                if (PlaceBombInGrid(index))
                    count--;
            }
        }

        private bool PlaceBombInGrid(int index)
        {
            int y = -1;
            int x = -1;

            // Check for existence
            if (BombList.Contains(index))
                return false;

            // Add to list
            BombList.Add(index);

            // Place in grid
            y = index / WidthTiles + 1;
            Math.DivRem(index, WidthTiles, out x);
            if (x == 0)
                x = 9;
            else
                x = index % WidthTiles;

            // Flag in grid
            Grid[y, x] = -1;

            // Change picture
            SetTileImage(string.Concat("tile_", y.ToString(), "_", x.ToString()), Resources.bomb);

            return true;
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void optionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GenerateNewGame();
        }
    }
}
