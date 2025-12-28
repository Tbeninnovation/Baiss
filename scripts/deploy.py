import os
import sys
import glob
import logging
import subprocess
import shutil
from build import get_downloads_dir
from build import CURRENT_BAISS_DESKTOP_VERSION, is_latest_version
import json

logging.basicConfig(level=logging.INFO, format='[%(levelname)s] %(message)s')
logger = logging.getLogger(__name__)

def sh(cmd: list[str], **kwargs) -> str:
    """Run a shell command and log its output."""
    logger.info("Running command: %s", " ".join(cmd))
    result = subprocess.run(cmd, text=True, capture_output=True, check=True, **kwargs)
    if result.stdout:
        logger.info(result.stdout)
    if result.stderr:
        logger.warning(result.stderr)
    return result.stdout.strip()


def deploy():
    release_tag = CURRENT_BAISS_DESKTOP_VERSION
    logger.info(f"Deploying to GitHub Release: {release_tag}")

    # Gather artifacts
    downloads_dir = get_downloads_dir()
    artifacts = []
    
    # Check for zip, msi, exe, dmg, pkg files
    extensions = ["*.zip", "*.msi", "*.exe", "*.dmg", "*.pkg"]
    for ext in extensions:
        artifacts.extend(glob.glob(os.path.join(downloads_dir, ext)))

    if not artifacts:
        logger.error("No artifacts found to deploy.")
        return 1

    logger.info(f"Found {len(artifacts)} artifacts: {artifacts}")

    # Check if release exists
    release_exists = False
    is_latest = is_latest_version()
    try:
        sh(["gh", "release", "view", release_tag])
        release_exists = True
        logger.info(f"Release {release_tag} already exists.")
    except subprocess.CalledProcessError:
        logger.info(f"Release {release_tag} does not exist.")

    # check for release notes
    release_notes_file = "release_notes.md"
    if not os.path.exists(release_notes_file):
        logger.error("Release notes file not found.")
        return 1
    
    try:
        if not release_exists:
            logger.info(f"Creating release {release_tag}...")
            # Create release
            # safe to use --generate-notes
            cmd = ["gh", "release", "create", release_tag] + artifacts + ["--notes-file", release_notes_file, "--title", release_tag]
            if not is_latest:
                cmd.append("--latest=false")
            sh(cmd)
        else:
            # TODO : Update this part for sure 
            logger.info(f"Updating release {release_tag}...")
            # override release
            # Update release metadata
            cmd_edit = ["gh", "release", "edit", release_tag, "--notes-file", release_notes_file, "--title", release_tag]
            if not is_latest:
                cmd_edit.append("--latest=false")
            sh(cmd_edit)
            
            # Upload artifacts
            logger.info(f"Uploading artifacts to {release_tag}...")
            cmd_upload = ["gh", "release", "upload", release_tag] + artifacts + ["--clobber"]

            sh(cmd_upload)
        
        logger.info("Deployment successful!")
    except subprocess.CalledProcessError as e:
        logger.error(f"Deployment failed: {e}")
        if e.stderr:
            logger.error(f"Stderr: {e.stderr}")
        return 1

    return 0

if __name__ == "__main__":
    exit(deploy())