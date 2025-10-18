using Cognex.VisionPro;
using Cognex.VisionPro.ImageFile;
using Cognex.VisionPro.ToolBlock;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ColorFuse
{
    public partial class Form1 : Form
    {
        // 成员变量
        private string currentImagePath = string.Empty;
        private List<string> imageFiles = new List<string>();
        private int currentImageIndex = -1;
        private string vppurl = "../../lib/ColorFuse.vpp";
        private CogToolBlock toolBlock = new CogToolBlock();

        public Form1()
        {
            InitializeComponent();
            string currentDirectory = Directory.GetCurrentDirectory();
            LoadToolBlock(); // 初始化时加载工具块
        }

        /// <summary>
        /// 加载VisionPro工具块文件
        /// </summary>
        private void LoadToolBlock()
        {
            try
            {

                // 加载工具块
                toolBlock = CogSerializer.LoadObjectFromFile(vppurl) as CogToolBlock;

                if (toolBlock != null)
                {
                    MessageBox.Show($"成功加载VPP文件：{Path.GetFileName(vppurl)}", "成功",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information);
                    this.Text = this.Text+$"已经成功加载{Path.GetFileName(vppurl)}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载VPP文件失败：{ex.Message}", "错误",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
#if DEBUG
                Console.WriteLine($"详细错误：{ex}");
                Environment.Exit(0);
#endif
            }
        }

        /// <summary>
        /// 选择图片文件夹按钮事件
        /// </summary>
        private void button1_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "选择包含图片的文件夹";
                folderDialog.SelectedPath = @"C:\";

                if (folderDialog.ShowDialog() == DialogResult.OK)
                {
                    LoadImagesFromFolder(folderDialog.SelectedPath);
                }
            }
        }

        /// <summary>
        /// 从指定文件夹加载所有支持的图片文件
        /// </summary>
        /// <param name="folderPath">文件夹路径</param>
        private void LoadImagesFromFolder(string folderPath)
        {
            try
            {
                // 支持的图片格式
                string[] extensions = { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.tif", "*.tiff" };

                // 获取所有图片文件
                var files = extensions.SelectMany(ext =>
                    Directory.GetFiles(folderPath, ext, SearchOption.TopDirectoryOnly)).ToList();

                if (files.Count > 0)
                {
                    files.Sort(); // 按文件名排序

                    // 更新图片列表和索引
                    imageFiles = files;
                    currentImageIndex = 0;
                    currentImagePath = files[0];

                    // 更新界面
                    UpdateStatusLabel($"已选择文件夹: {Path.GetFileName(folderPath)} (共{files.Count}个图片文件)");
                    PreviewImage(currentImagePath);

                    // 自动对第一张图片进行匹配处理
                    ProcessCurrentImage();

                    MessageBox.Show($"成功加载 {files.Count} 个图片文件", "加载成功",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("选择的文件夹中没有找到支持的图片文件", "提示",
                                  MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取图片文件时出错: {ex.Message}", "错误",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 预览指定路径的图片
        /// </summary>
        /// <param name="imagePath">图片路径</param>
        private void PreviewImage(string imagePath)
        {
            try
            {
                currentImagePath = imagePath;
                cogRecordDisplay1.Image = null; // 清空之前显示

                using (CogImageFileTool imageLoader = new CogImageFileTool())
                {
                    imageLoader.Operator.Open(imagePath, CogImageFileModeConstants.Read);
                    imageLoader.Run();

                    if (imageLoader.OutputImage != null)
                    {
                        cogRecordDisplay1.Image = imageLoader.OutputImage;
                        cogRecordDisplay1.Fit(); // 自适应显示
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"预览图片失败: {ex.Message}", "错误",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// 匹配按钮 - 对当前图片进行视觉处理
        /// </summary>
        private void button2_Click(object sender, EventArgs e)
        {
            ProcessCurrentImage();
        }

        /// <summary>
        /// 处理当前图片的核心方法（被button2和导航按钮调用）
        /// </summary>
        private void ProcessCurrentImage()
        {
            // 前置检查
            if (!ValidateBeforeProcessing()) return;

            string currentImage = imageFiles[currentImageIndex];

            // 使用VisionPro处理图片
            using (CogImageFileTool imageFileTool = new CogImageFileTool())
            {
                try
                {
                    // 加载图片
                    imageFileTool.Operator.Open(currentImage, CogImageFileModeConstants.Read);
                    imageFileTool.Run();

                    if (imageFileTool.OutputImage == null)
                    {
                        MessageBox.Show("图片加载失败，无法进行处理", "错误",
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    // 设置工具块输入并运行
                    if (SetToolBlockInput(imageFileTool.OutputImage))
                    {
                        RunToolBlockAndDisplayResult();
                    }
                }
                finally
                {
                    imageFileTool.Operator?.Close();
                }
            }
        }

        /// <summary>
        /// 处理前的验证检查
        /// </summary>
        /// <returns>是否通过验证</returns>
        private bool ValidateBeforeProcessing()
        {
            if (imageFiles.Count == 0)
            {
                MessageBox.Show("请先选择包含图片的文件夹", "提示",
                              MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            if (currentImageIndex < 0 || currentImageIndex >= imageFiles.Count)
            {
                MessageBox.Show("当前图片索引无效", "错误",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            if (toolBlock == null)
            {
                MessageBox.Show("视觉工具块未正确加载", "错误",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 设置工具块输入参数
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <returns>是否设置成功</returns>
        private bool SetToolBlockInput(ICogImage inputImage)
        {
            // 尝试不同的输入参数名称
            string[] possibleInputNames = { "OutputImage", "InputImage", "Image", "inputImage" };

            foreach (string inputName in possibleInputNames)
            {
                if (toolBlock.Inputs.Contains(inputName))
                {
                    toolBlock.Inputs[inputName].Value = inputImage;
                    return true;
                }
            }

            // 如果找不到合适的输入参数，显示可用参数
            string availableInputs = string.Join(", ",
                toolBlock.Inputs.Cast<CogToolBlockTerminal>().Select(t => t.Name));

            MessageBox.Show($"未找到合适的输入图像参数！\n可用参数：{availableInputs}",
                          "配置错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }

        /// <summary>
        /// 运行工具块并显示结果
        /// </summary>
        private void RunToolBlockAndDisplayResult()
        {
            toolBlock.Run();

            // 检查运行结果
            if (toolBlock.RunStatus.Result != CogToolResultConstants.Accept)
            {
                MessageBox.Show($"工具块运行失败：{toolBlock.RunStatus.Message}", "运行错误",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 显示处理结果
            var lastRunRecord = toolBlock.CreateLastRunRecord();
            if (lastRunRecord?.SubRecords.Count > 0)
            {
                cogRecordDisplay1.Record = lastRunRecord.SubRecords[0];
                cogRecordDisplay1.Fit();
                UpdateStatusLabel(); // 更新状态显示
            }
            else
            {
                MessageBox.Show("工具块未生成有效的结果记录", "警告",
                              MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        /// <summary>
        /// 下一张图片按钮 - 切换图片并自动进行匹配处理
        /// </summary>
        private void buttonNext_Click_Click(object sender, EventArgs e)
        {
            NavigateToNextImage();
        }

        /// <summary>
        /// 上一张图片按钮 - 切换图片并自动进行匹配处理
        /// </summary>
        private void buttonPrev_Click_1(object sender, EventArgs e)
        {
            NavigateToPreviousImage();
        }

        /// <summary>
        /// 导航到下一张图片并自动处理
        /// </summary>
        private void NavigateToNextImage()
        {
            if (imageFiles.Count > 0)
            {
                currentImageIndex = (currentImageIndex + 1) % imageFiles.Count;
                NavigateToImage();
            }
        }

        /// <summary>
        /// 导航到上一张图片并自动处理
        /// </summary>
        private void NavigateToPreviousImage()
        {
            if (imageFiles.Count > 0)
            {
                currentImageIndex = (currentImageIndex - 1 + imageFiles.Count) % imageFiles.Count;
                NavigateToImage();
            }
        }

        /// <summary>
        /// 导航到当前索引的图片并自动进行匹配处理
        /// </summary>
        private void NavigateToImage()
        {
            PreviewImage(imageFiles[currentImageIndex]);
            UpdateStatusLabel();
            ProcessCurrentImage(); // 自动调用匹配处理
        }

        /// <summary>
        /// 更新状态标签显示
        /// </summary>
        /// <param name="customText">自定义文本，为空时显示图片信息</param>
        private void UpdateStatusLabel(string customText = null)
        {
            if (!string.IsNullOrEmpty(customText))
            {
                label1.Text = customText;
            }
            else if (imageFiles.Count > 0 && currentImageIndex >= 0)
            {
                string fileName = Path.GetFileName(imageFiles[currentImageIndex]);
                label1.Text = $"当前图片: {fileName} ({currentImageIndex + 1}/{imageFiles.Count})";
            }
        }

        // 以下为空事件处理方法
        private void splitContainer1_Panel1_Paint(object sender, PaintEventArgs e) { }
        private void Form1_Load(object sender, EventArgs e) { }
        private void cogRecordDisplay1_Enter(object sender, EventArgs e) { }
        private void UpdateNavigationButtons() { } // 预留导航按钮状态更新方法
    }
}