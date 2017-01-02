param (
    # The csproj file name
    [Parameter(Mandatory=$true)][string]$csproj,
    # The build number (from AppVeyor)
    [Parameter(Mandatory=$true)][int]$build,
    # True if this is a tagged release. False for pre-release.
    [switch]$release
)

function GetPackageVersionNode ([xml] $csXml)
{
    # find the PackageVersion element
    $nodes = $csXml.GetElementsByTagName("PackageVersion")
    if ($nodes.Count -eq 0)
    {
        Write-Error "$csproj does not contain a <PackageVersion> element."
        Exit 1
    }

    if ($nodes.Count -gt 1)
    {
        Write-Error "$csproj contains more than one instance of <PackageVersion>"
        Exit 1
    }

    return $nodes[0]
}

function GetMajorVersion ([string] $version)
{
    # validate the package version and extract major version
    $versionMatch = [Regex]::Match($version, '^(?<Major>\d+)\.(?<Minor>\d+)\.(?<Patch>\d+)$')
    if (!$versionMatch.Success)
    {
        Write-Error "Invalid PackageVersion: $version. It must follow the MAJOR.MINOR.PATCH format."
        Exit 1
    }

    return $versionMatch.Groups["Major"].Value
}

function GeneratePrereleaseVersion ([string] $version)
{
	# we want to increment the patch number for unstable builds
	$version = [Regex]::Replace($version, '^(\d+\.\d+\.)(\d+)$', {
		param([System.Text.RegularExpressions.Match] $match)
		$val = [int]::Parse($match.Groups[2].Value)
		$val++
		$match.Groups[1].Value + $val
	})

	$version += "-unstable$build"

    return $version
}

function InsertOrUpdateElement([System.Xml.XmlNode] $parent, [string] $tag, [string] $value)
{
    foreach ($n in $parent.ChildNodes)
    {
        if ($n.Name -eq $tag)
        {
            Write-Output "Updating: $tag = $value"
            $n.InnerXml = $value
            return
        }
    }

    # the element doesn't exist, we need to insert it
    Write-Output "Inserting: $tag = $value"
    $node = $parent.OwnerDocument.CreateElement($tag, $parent.NamespaceURI)
    $node.InnerXml = $value
    $parent.AppendChild($node) | Out-Null
}

function Run
{
    # read csproj file
    $fileName = Join-Path -Path (Get-Location) -ChildPath $csproj
    $csXml = New-Object XML
    $csXml.Load($fileName)

    $packageVersionNode = GetPackageVersionNode $csXml
    $version = $packageVersionNode.InnerText

    $majorVersion = GetMajorVersion $version

    if (!$release)
    {
        $version = GeneratePrereleaseVersion $version
    }
    
    $parent = $packageVersionNode.ParentNode

    InsertOrUpdateElement $parent 'PackageVersion' $version
    InsertOrUpdateElement $parent 'Version' $version
    InsertOrUpdateElement $parent 'FileVersion' $build.ToString()
    InsertOrUpdateElement $parent 'AssemblyVersion' $majorVersion

    $csXml.Save($fileName)
}

Run