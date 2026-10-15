"""Pin manual distribution signing onto the targets of a Unity-exported Xcode project.

Build settings passed on the xcodebuild command line apply to every target, and Xcode rejects a
provisioning profile on UnityFramework, so the profile has to be set per target instead. The project
file must already be converted to XML (`plutil -convert xml1`), which Xcode reads natively.

Usage: configure_signing.py <project.pbxproj>
Reads APPLE_TEAM_ID, SIGNING_IDENTITY, PROFILE_UUID and BUNDLE_ID from the environment.
"""

import os
import plistlib
import sys

APP_TARGET = "Unity-iPhone"
FRAMEWORK_TARGET = "UnityFramework"


def target_build_settings(objects, target):
    configuration_list = objects[target["buildConfigurationList"]]
    return [objects[key]["buildSettings"] for key in configuration_list["buildConfigurations"]]


def main():
    project_path = sys.argv[1]
    team_id = os.environ["APPLE_TEAM_ID"]
    identity = os.environ["SIGNING_IDENTITY"]
    profile_uuid = os.environ["PROFILE_UUID"]
    bundle_id = os.environ["BUNDLE_ID"]

    with open(project_path, "rb") as project_file:
        project = plistlib.load(project_file)

    objects = project["objects"]
    targets = {
        entry["name"]: entry for entry in objects.values() if entry.get("isa") == "PBXNativeTarget"
    }

    for required in (APP_TARGET, FRAMEWORK_TARGET):
        if required not in targets:
            sys.exit(f"Target '{required}' not found in {project_path}.")

    shared = {
        "CODE_SIGN_STYLE": "Manual",
        "DEVELOPMENT_TEAM": team_id,
        "CODE_SIGN_IDENTITY": identity,
        "CODE_SIGN_IDENTITY[sdk=iphoneos*]": identity,
    }

    for settings in target_build_settings(objects, targets[APP_TARGET]):
        project_bundle_id = settings.get("PRODUCT_BUNDLE_IDENTIFIER")
        if project_bundle_id != bundle_id:
            sys.exit(
                f"Provisioning profile is for '{bundle_id}' but the project builds '{project_bundle_id}'."
            )
        settings.update(shared)
        settings["PROVISIONING_PROFILE_SPECIFIER"] = profile_uuid
        settings["PROVISIONING_PROFILE"] = ""

    for settings in target_build_settings(objects, targets[FRAMEWORK_TARGET]):
        settings.update(shared)
        settings["PROVISIONING_PROFILE_SPECIFIER"] = ""
        settings["PROVISIONING_PROFILE"] = ""

    with open(project_path, "wb") as project_file:
        plistlib.dump(project, project_file, fmt=plistlib.FMT_XML)

    print(f"Configured manual signing for {APP_TARGET} and {FRAMEWORK_TARGET}.")


if __name__ == "__main__":
    main()
