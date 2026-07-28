Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$scriptDir = Split-Path -Path $MyInvocation.MyCommand.Path -Parent
$wpfLauncherPath = Join-Path $scriptDir 'dist\ProjectLauncher.Wpf.exe'
if (Test-Path -LiteralPath $wpfLauncherPath) {
    Start-Process -FilePath $wpfLauncherPath
    exit
}

$aiToolsHome = if ([string]::IsNullOrWhiteSpace($env:AI_TOOLS_HOME)) {
    Join-Path $env:USERPROFILE 'ai-tools'
}
else {
    [System.IO.Path]::GetFullPath($env:AI_TOOLS_HOME)
}
$projectsFile = Join-Path $aiToolsHome 'launch-projects.json'
$codePath = 'C:\Users\Krzysztof\AppData\Local\Programs\Microsoft VS Code\Code.exe'

function Initialize-ProjectsFile {
    if (-not (Test-Path -LiteralPath $aiToolsHome)) {
        New-Item -ItemType Directory -Force -Path $aiToolsHome | Out-Null
    }

}

function Load-Projects {
    Initialize-ProjectsFile

    if (-not (Test-Path -LiteralPath $projectsFile)) {
        [System.Windows.Forms.MessageBox]::Show(
            "Nie znaleziono konfiguracji projektow:`n$projectsFile",
            'Projekty',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error
        ) | Out-Null
        exit 1
    }

    $json = Get-Content -LiteralPath $projectsFile -Raw -Encoding UTF8
    return @($json | ConvertFrom-Json)
}

function Save-Projects {
    param(
        [Parameter(Mandatory = $true)]
        [object[]] $Projects
    )

    $Projects |
        ConvertTo-Json -Depth 4 |
        Set-Content -LiteralPath $projectsFile -Encoding UTF8
}

$projects = Load-Projects

function Open-Project {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ProjectName
    )

    $project = $projects | Where-Object { $_.name -eq $ProjectName } | Select-Object -First 1
    if ($null -eq $project) {
        return
    }

    if (-not (Test-Path -LiteralPath $codePath)) {
        [System.Windows.Forms.MessageBox]::Show(
            "Nie znaleziono Visual Studio Code:`n$codePath",
            'Projekty',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error
        ) | Out-Null
        return
    }

    if (-not (Test-Path -LiteralPath $project.path)) {
        [System.Windows.Forms.MessageBox]::Show(
            "Nie znaleziono projektu:`n$($project.path)",
            'Projekty',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error
        ) | Out-Null
        return
    }

    $project.lastLaunched = Get-Date -Format 'yyyy-MM-dd HH:mm'
    Save-Projects -Projects $projects

    Start-Process -FilePath $codePath -ArgumentList "--new-window `"$($project.path)`""
    $form.Close()
}

function Format-LastLaunched {
    param(
        [string] $LastLaunched
    )

    if ([string]::IsNullOrWhiteSpace($LastLaunched)) {
        return ''
    }

    return $LastLaunched
}

function Measure-WrappedTextHeight {
    param(
        [string] $Text,
        [System.Drawing.Font] $Font,
        [int] $Width
    )

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return 0
    }

    $proposedSize = New-Object System.Drawing.Size($Width, 1000)
    $flags = [System.Windows.Forms.TextFormatFlags]::WordBreak -bor [System.Windows.Forms.TextFormatFlags]::TextBoxControl
    $measuredSize = [System.Windows.Forms.TextRenderer]::MeasureText($Text, $Font, $proposedSize, $flags)
    return $measuredSize.Height
}

function Add-ProjectCard {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Project,

        [Parameter(Mandatory = $true)]
        [int] $ProjectNumber,

        [Parameter(Mandatory = $true)]
        [int] $Top
    )

    $cardWidth = 640
    $contentLeft = 14
    $contentWidth = $cardWidth - 28
    $dateWidth = 150
    $descriptionFont = New-Object System.Drawing.Font('Segoe UI', 9)
    $descriptionHeight = Measure-WrappedTextHeight -Text $Project.description -Font $descriptionFont -Width $contentWidth
    $cardHeight = [Math]::Max(76, 68 + $descriptionHeight)

    $panel = New-Object System.Windows.Forms.Panel
    $panel.Tag = $Project.name
    $panel.Location = New-Object System.Drawing.Point(16, $Top)
    $panel.Size = New-Object System.Drawing.Size($cardWidth, $cardHeight)
    $panel.BackColor = [System.Drawing.Color]::FromArgb(42, 42, 42)
    $panel.Cursor = [System.Windows.Forms.Cursors]::Hand

    $title = New-Object System.Windows.Forms.Label
    $title.Text = "{0}. {1}" -f $ProjectNumber, $Project.name
    $title.Location = New-Object System.Drawing.Point($contentLeft, 10)
    $title.Size = New-Object System.Drawing.Size(($contentWidth - $dateWidth - 12), 24)
    $title.ForeColor = [System.Drawing.Color]::White
    $title.Font = New-Object System.Drawing.Font('Segoe UI Semibold', 11)
    $title.BackColor = [System.Drawing.Color]::Transparent

    $lastLaunched = New-Object System.Windows.Forms.Label
    $lastLaunched.Text = Format-LastLaunched -LastLaunched $Project.lastLaunched
    $lastLaunched.Location = New-Object System.Drawing.Point(($cardWidth - $dateWidth - 14), 12)
    $lastLaunched.Size = New-Object System.Drawing.Size($dateWidth, 18)
    $lastLaunched.ForeColor = [System.Drawing.Color]::FromArgb(165, 165, 165)
    $lastLaunched.Font = New-Object System.Drawing.Font('Segoe UI', 8)
    $lastLaunched.TextAlign = [System.Drawing.ContentAlignment]::TopRight
    $lastLaunched.BackColor = [System.Drawing.Color]::Transparent

    $path = New-Object System.Windows.Forms.Label
    $path.Text = $Project.path
    $path.Location = New-Object System.Drawing.Point($contentLeft, 36)
    $path.Size = New-Object System.Drawing.Size($contentWidth, 20)
    $path.ForeColor = [System.Drawing.Color]::FromArgb(190, 190, 190)
    $path.Font = New-Object System.Drawing.Font('Segoe UI', 9)
    $path.BackColor = [System.Drawing.Color]::Transparent

    $description = New-Object System.Windows.Forms.Label
    $description.Text = $Project.description
    $description.Location = New-Object System.Drawing.Point($contentLeft, 58)
    $description.Size = New-Object System.Drawing.Size($contentWidth, $descriptionHeight)
    $description.ForeColor = [System.Drawing.Color]::FromArgb(222, 222, 222)
    $description.Font = $descriptionFont
    $description.BackColor = [System.Drawing.Color]::Transparent

    $clickHandler = { Open-Project -ProjectName $this.Tag }
    $panel.Add_Click($clickHandler)
    foreach ($control in @($title, $lastLaunched, $path, $description)) {
        $control.Tag = $Project.name
        $control.Cursor = [System.Windows.Forms.Cursors]::Hand
        $control.Add_Click($clickHandler)
        [void] $panel.Controls.Add($control)
    }

    [void] $form.Controls.Add($panel)
    return $cardHeight
}

$form = New-Object System.Windows.Forms.Form
$form.Text = 'Projekty'
$form.StartPosition = 'CenterScreen'
$form.FormBorderStyle = 'FixedSingle'
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.ShowInTaskbar = $true
$form.BackColor = [System.Drawing.Color]::FromArgb(31, 31, 31)
$form.ForeColor = [System.Drawing.Color]::White
$form.ClientSize = New-Object System.Drawing.Size(672, 120)
$form.Font = New-Object System.Drawing.Font('Segoe UI', 11)
$form.KeyPreview = $true
$form.AutoScroll = $true

$iconPath = $codePath
if (Test-Path -LiteralPath $iconPath) {
    try {
        $form.Icon = [System.Drawing.Icon]::ExtractAssociatedIcon($iconPath)
    }
    catch {
        # Window icon is cosmetic; keep the launcher working if extraction fails.
    }
}

$y = 14
$projectNumber = 1
foreach ($project in $projects) {
    $cardHeight = Add-ProjectCard -Project $project -ProjectNumber $projectNumber -Top $y
    $y += $cardHeight + 10
    $projectNumber += 1
}

$desiredHeight = [Math]::Min(760, ($y + 4))
$form.ClientSize = New-Object System.Drawing.Size(672, $desiredHeight)

$form.Add_KeyDown({
    if ($_.KeyCode -eq [System.Windows.Forms.Keys]::Escape) {
        $form.Close()
        return
    }

    $key = $_.KeyCode.ToString()
    $projectNumber = $null
    if ($key -match '^D([1-9])$') {
        $projectNumber = [int] $Matches[1]
    }
    elseif ($key -match '^NumPad([1-9])$') {
        $projectNumber = [int] $Matches[1]
    }

    if ($null -ne $projectNumber -and $projectNumber -le $projects.Count) {
        Open-Project -ProjectName $projects[$projectNumber - 1].name
    }
})

[System.Windows.Forms.Application]::EnableVisualStyles()
[System.Windows.Forms.Application]::Run($form)
