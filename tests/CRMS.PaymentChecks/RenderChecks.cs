using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CRMS.Services.Payments;
using CRMS.Views.Shared;

internal static class RenderChecks
{
    public static void Run(string output)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                Directory.CreateDirectory(output);
                var app = new CRMS.App(); app.InitializeComponent();
                foreach (var mode in new[] { "Balance", "Refund", "Summary" })
                {
                    var control = new PaymentWorkspace();
                    var vm = new CRMS.ViewModels.PaymentWorkspaceViewModel(mode)
                    {
                        BookingLookup = "TV3-DEMO-001", RefundAmount = "450000", ApprovalReference = "DUYET-DEMO-001"
                    };
                    var summary = new PaymentSummary(1,"TV3-DEMO-001",mode == "Refund" ? "Cancelled" : "Returned",900000,100000,450000,70000,450000,0,0,true);
                    typeof(CRMS.ViewModels.PaymentWorkspaceViewModel).GetProperty("Summary")!.SetValue(vm, summary);
                    typeof(CRMS.ViewModels.PaymentWorkspaceViewModel).GetProperty("Message")!.SetValue(vm,"Ảnh kiểm tra bố cục với dữ liệu mẫu; không phải giao dịch thật.");
                    control.DataContext = vm;
                    var root = new System.Windows.Controls.Border { Background=new SolidColorBrush(Color.FromRgb(244,247,251)), Child=control };
                    root.Measure(new Size(850,1000)); root.Arrange(new Rect(0,0,850,1000)); root.UpdateLayout();
                    Dispatcher.CurrentDispatcher.Invoke(()=>{}, DispatcherPriority.ApplicationIdle); root.UpdateLayout();
                    var bitmap = new RenderTargetBitmap(850,1000,96,96,PixelFormats.Pbgra32); bitmap.Render(root);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream=File.Create(Path.Combine(output,$"TV3-{mode}.png")); encoder.Save(stream);
                }
            }
            catch(Exception ex){error=ex;}
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if(error is not null) throw error;
    }
}
