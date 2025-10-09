namespace torra_watch
{
    partial class MainForm   // <-- same name & same namespace as above
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            SuspendLayout();
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            ForeColor = Color.Black;
            Name = "MainForm";
            Text = "Torra Watch";
            ResumeLayout(false);
        }
    }
}
