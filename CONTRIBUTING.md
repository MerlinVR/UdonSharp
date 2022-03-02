# Welcome to the UdonSharp contributing guide

UdonSharp is an open source C# compiler for VRChat's Udon scripting system. Contributions are welcome and greatly appreciated.

## Reporting Issues
Before reporting an issue, make sure you have installed UdonSharp following the [installation instructions](https://github.com/vrchat-community/UdonSharp#installation). If you run into an issue with UdonSharp, report it on the [issues](https://github.com/vrchat-community/UdonSharp/issues/new/choose) page. 

## Contributing to UdonSharp
UdonSharp accepts [pull requests](https://docs.github.com/en/pull-requests/collaborating-with-pull-requests/proposing-changes-to-your-work-with-pull-requests/about-pull-requests). The first time you contribute to a vrchat-community repository, you will be asked to review and sign VRChat's Contributor License Agreement. This will be requested automatically when you submit your first pull request.

### Testing your PR
If you are making changes to the compiler, it is recommended that you run U#'s integration tests after running a U# compile with your changes. We are working on improving U#'s testing process, at the moment there is some work involved:

1. Clone the UdonSharp repository 
2. Integrate your changes
3. Remove the trailing `~` from the `Assets/UdonSharp/Samples~` and `Assets/UdonSharp/Tests~` directories
4. Open the UdonSharp project in Unity 2019.4.31f1
5. Install the latest [VRChat worlds SDK](https://vrchat.com/home/download) in the project
6. Open the `IntegrationTestScene` scene in the `Assets/UdonSharp/Tests` directory
7. Run a manual U# compile from the button on UdonSharpProgramAsset files 
8. Enter play mode and verify that all tests are passing in the output log

If you are contributing compiler fixes or features, it's encouraged to add tests to the test scripts to help prevent regressions.
