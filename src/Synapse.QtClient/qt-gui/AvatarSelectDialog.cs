// ------------------------------------------------------------------------------
//  <autogenerated>
//      This code was generated by a tool.
//      Mono Runtime Version: 2.0.50727.42
// 
//      Changes to this file may cause incorrect behavior and will be lost if 
//      the code is regenerated.
//  </autogenerated>
// ------------------------------------------------------------------------------

namespace Synapse.QtClient.Windows {
    using System;
    using Qyoto;
    
    
    public partial class AvatarSelectDialog : QDialog {
        
        protected QLabel label_2;
        
        protected QLabel avatarLabel;
        
        protected QPushButton browseButton;
        
        protected QPushButton clearButton;
        
        protected QFrame line;
        
        protected QLabel label_3;
        
        protected QLineEdit lineEdit;
        
        protected QPushButton searchButton;
        
        protected QTabWidget tabWidget;
        
        protected QDialogButtonBox buttonBox;
        
        protected void SetupUi() {
            base.ObjectName = "AvatarSelectDialog";
            this.Geometry = new QRect(0, 0, 542, 361);
            this.WindowTitle = "Select Avatar";
            QVBoxLayout verticalLayout_2;
            verticalLayout_2 = new QVBoxLayout(this);
            QHBoxLayout horizontalLayout;
            horizontalLayout = new QHBoxLayout();
            verticalLayout_2.AddLayout(horizontalLayout);
            QVBoxLayout verticalLayout_3;
            verticalLayout_3 = new QVBoxLayout();
            horizontalLayout.AddLayout(verticalLayout_3);
            verticalLayout_3.sizeConstraint = QLayout.SizeConstraint.SetMinimumSize;
            this.label_2 = new QLabel(this);
            this.label_2.ObjectName = "label_2";
            this.label_2.Text = "Your Avatar:";
            verticalLayout_3.AddWidget(this.label_2);
            this.avatarLabel = new QLabel(this);
            this.avatarLabel.ObjectName = "avatarLabel";
            this.avatarLabel.MinimumSize = new QSize(48, 96);
            this.avatarLabel.FrameShape = QFrame.Shape.StyledPanel;
            this.avatarLabel.FrameShadow = QFrame.Shadow.Raised;
            this.avatarLabel.Text = "";
            this.avatarLabel.Alignment = global::Qyoto.Qyoto.GetCPPEnumValue("Qt", "AlignCenter");
            verticalLayout_3.AddWidget(this.avatarLabel);
            this.browseButton = new QPushButton(this);
            this.browseButton.ObjectName = "browseButton";
            this.browseButton.Text = "Select File...";
            verticalLayout_3.AddWidget(this.browseButton);
            this.clearButton = new QPushButton(this);
            this.clearButton.ObjectName = "clearButton";
            this.clearButton.Text = "Clear";
            verticalLayout_3.AddWidget(this.clearButton);
            QSpacerItem verticalSpacer;
            verticalSpacer = new QSpacerItem(20, 40, QSizePolicy.Policy.Minimum, QSizePolicy.Policy.Expanding);
            verticalLayout_3.AddItem(verticalSpacer);
            this.line = new QFrame(this);
            this.line.ObjectName = "line";
            this.line.FrameShape = QFrame.Shape.VLine;
            this.line.FrameShadow = QFrame.Shadow.Sunken;
            horizontalLayout.AddWidget(this.line);
            QVBoxLayout verticalLayout;
            verticalLayout = new QVBoxLayout();
            horizontalLayout.AddLayout(verticalLayout);
            QGridLayout gridLayout;
            gridLayout = new QGridLayout();
            verticalLayout.AddLayout(gridLayout);
            this.label_3 = new QLabel(this);
            this.label_3.ObjectName = "label_3";
            this.label_3.Text = "Search:";
            this.label_3.SetBuddy(lineEdit);
            gridLayout.AddWidget(this.label_3, 0, 0, 1, 1);
            this.lineEdit = new QLineEdit(this);
            this.lineEdit.ObjectName = "lineEdit";
            gridLayout.AddWidget(this.lineEdit, 0, 1, 1, 1);
            this.searchButton = new QPushButton(this);
            this.searchButton.ObjectName = "searchButton";
            this.searchButton.Text = "Search";
            this.searchButton.Default = true;
            gridLayout.AddWidget(this.searchButton, 0, 2, 1, 1);
            this.tabWidget = new QTabWidget(this);
            this.tabWidget.ObjectName = "tabWidget";
            this.tabWidget.CurrentIndex = -1;
            verticalLayout.AddWidget(this.tabWidget);
            this.buttonBox = new QDialogButtonBox(this);
            this.buttonBox.ObjectName = "buttonBox";
            this.buttonBox.StandardButtons = global::Qyoto.Qyoto.GetCPPEnumValue("QDialogButtonBox", "Close");
            verticalLayout_2.AddWidget(this.buttonBox);
            QObject.Connect(buttonBox, Qt.SIGNAL("rejected()"), this, Qt.SLOT("reject()"));
            QMetaObject.ConnectSlotsByName(this);
        }
    }
}
